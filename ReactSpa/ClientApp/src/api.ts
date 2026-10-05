export interface WeatherForecast {
  date: string
  temperatureC: number
  temperatureF: number
  summary?: string
}

export type ApiMode = "live" | "demo"

let mode: ApiMode = "live"

/**
 * Whether the most recent call was actually served by the ASP.NET Core API.
 *
 * The GitHub Pages copy of this SPA is a static bundle with nothing behind
 * /api/*. A missing API (network error, or 404/405 from the static host) falls
 * back to data generated in the browser so the deployed demo shows something
 * useful. A genuine API failure (500, malformed payload) still rejects, so a
 * real server bug is never masked by the fallback.
 */
export const apiMode = (): ApiMode => mode

class ApiMissingError extends Error {}

// Resolve API calls against the Vite base. With the default base of "/" this is
// "/api/...", but the GitHub Pages build sets the base to "/DotNetCode/", so a
// hard-coded "/api/..." would ask the domain root instead of the project site.
const api = (path: string) => `${import.meta.env.BASE_URL}api/${path}`

async function handle<T>(response: Response): Promise<T> {
  if (response.status === 404 || response.status === 405) {
    throw new ApiMissingError(`HTTP ${response.status}`)
  }
  if (!response.ok) {
    throw new Error(`HTTP ${response.status} ${response.statusText}`)
  }
  return response.json() as Promise<T>
}

function isMissingApi(err: unknown): boolean {
  // A TypeError here means fetch itself failed: no host, offline, or CORS.
  return err instanceof ApiMissingError || err instanceof TypeError
}

const summaries = [
  "Freezing", "Bracing", "Chilly",
  "Cool", "Mild", "Warm", "Balmy",
  "Hot", "Sweltering", "Scorching",
]

// Mirrors /api/weather in ReactSpa.Api/Program.cs, including the odd
// 32 + trunc(c / 0.5556) Fahrenheit conversion, so demo output matches.
function demoWeather(): WeatherForecast[] {
  const pad = (n: number) => String(n).padStart(2, "0")
  return Array.from({ length: 5 }, (_, i) => {
    const date = new Date()
    date.setDate(date.getDate() + i + 1)
    // Random.Shared.Next(-20, 55) in the API.
    const temperatureC = Math.floor(Math.random() * 75) - 20
    return {
      date: `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`,
      temperatureC,
      temperatureF: 32 + Math.trunc(temperatureC / 0.5556),
      summary: summaries[Math.floor(Math.random() * summaries.length)],
    }
  })
}

export async function getWeather(): Promise<WeatherForecast[]> {
  try {
    const data = await fetch(api("weather")).then((r) => handle<WeatherForecast[]>(r))
    mode = "live"
    return data
  } catch (err) {
    if (!isMissingApi(err)) throw err
    mode = "demo"
    return demoWeather()
  }
}

export async function echoMessage(message: string): Promise<string> {
  try {
    const data = await fetch(api("echo"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ message }),
    }).then((r) => handle<{ message: string }>(r))
    mode = "live"
    return data.message
  } catch (err) {
    if (!isMissingApi(err)) throw err
    mode = "demo"
    return message
  }
}

