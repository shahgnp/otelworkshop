using System.Net;

using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };

try
{
    using var response = await client.GetAsync("http://127.0.0.1:8080/");
    return response.StatusCode == HttpStatusCode.OK ? 0 : 1;
}
catch (HttpRequestException)
{
    return 1;
}