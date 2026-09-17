namespace ReactSpa.Api;

public sealed record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public sealed record EchoRequest(string Message);

public sealed record EchoResponse(string Message);