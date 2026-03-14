using System.Text;

string apiUrl = "http://192.168.49.2:30000/api/telemetry";
using var client = new HttpClient();

var random = new Random();
int requestCount = 0;

Parallel.For(0, 100, new ParallelOptions
{
    MaxDegreeOfParallelism = 20
}, async (i) =>
{
    while (true)
    {
        string json = $$"""
        {
            "SensorId": "Czujnik-Test-{{random.Next(1, 1000)}}",
            "Temperature": {{random.Next(20, 80)}},
            "Timestamp": "{{DateTime.UtcNow.ToString("O")}}"
        }
        """;

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            HttpResponseMessage response = await client.PostAsync(apiUrl, content);
            int currentCount = Interlocked.Increment(ref requestCount);

            if (currentCount % 100 == 0)
            {
                Console.WriteLine($"Send {currentCount} queries. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
});

Console.ReadLine();