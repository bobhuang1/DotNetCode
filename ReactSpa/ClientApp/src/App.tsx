import { useEffect, useState } from 'react'
import { echoMessage, getWeather, type WeatherForecast } from './api'

export default function App() {
  const [weather, setWeather] = useState<WeatherForecast[]>([])
  const [loading, setLoading] = useState(false)
  const [draft, setDraft] = useState('')
  const [echo, setEcho] = useState('')
  const [error, setError] = useState('')

  const refresh = async () => {
    setLoading(true)
    setError('')
    try {
      setWeather(await getWeather())
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void refresh()
  }, [])

  const sendEcho = async () => {
    if (!draft) return
    setError('')
    try {
      setEcho(await echoMessage(draft))
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err))
    }
  }

  return (
    <main className="container">
      <h1>React SPA on ASP.NET Core 10</h1>
      <p>
        This single page is served by the Vite dev server during development and
        by ASP.NET Core from <code>wwwroot</code> in a published build. API calls
        go to <code>/api/*</code> either way.
      </p>

      <section>
        <h2>Weather from <code>/api/weather</code></h2>
        {loading && <p>Loading…</p>}
        {error && <p className="error">{error}</p>}
        {!loading && !error && (
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Temp (°C)</th>
                <th>Temp (°F)</th>
                <th>Summary</th>
              </tr>
            </thead>
            <tbody>
              {weather.map((w) => (
                <tr key={w.date}>
                  <td>{w.date}</td>
                  <td>{w.temperatureC}</td>
                  <td>{w.temperatureF}</td>
                  <td>{w.summary}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <button onClick={() => void refresh()} disabled={loading}>
          Refresh
        </button>
      </section>

      <section>
        <h2>Echo to <code>/api/echo</code></h2>
        <div className="row">
          <input
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && void sendEcho()}
            placeholder="Type something…"
          />
          <button onClick={() => void sendEcho()}>Send</button>
        </div>
        {echo && <p>Server said: <strong>{echo}</strong></p>}
      </section>
    </main>
  )
}