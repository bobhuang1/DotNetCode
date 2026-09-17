export interface WeatherForecast {
  date: string
  temperatureC: number
  temperatureF: number
  summary?: string
}

async function handle<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new Error(`HTTP ${response.status} ${response.statusText}`)
  }
  return response.json() as Promise<T>
}

export function getWeather(): Promise<WeatherForecast[]> {
  return fetch('/api/weather').then((r) => handle<WeatherForecast[]>(r))
}

export function echoMessage(message: string): Promise<string> {
  return fetch('/api/echo', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message }),
  }).then((r) => handle<{ message: string }>(r).then((d) => d.message))
}