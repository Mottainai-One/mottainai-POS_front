using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;

namespace PDVMottainai.Services
{
    public static class InternetConnectivityService
    {
        private static readonly HttpClient Client = new()
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        public static async Task<bool> HasInternetAccessAsync(CancellationToken cancellationToken = default)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
                return false;

            try
            {
                using var response = await Client.GetAsync(
                    "https://www.google.com/generate_204",
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                return response.StatusCode == HttpStatusCode.NoContent;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }
    }
}
