using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace ParserParallel;

public class UrlParser
{
    private static readonly string[] FileTypes = [
            "js", "css", "png", "jpg", "jpeg", 
            "gif", "svg", "webp", "mp4", "webm", 
            "ogg", "mp3", "wav", "pdf", "ico", 
            "woff", "woff2", "ttf", "otf"];

    private static readonly Regex HrefPattern = new(
        """\b((http|https|ftp):\/\/[-\w@:%_\+.~#?&//=]+|www\.[-\w@:%_\+.~#?&//=]+(\.[a-z]{2,6})?)""", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(10)
    );
    
    private static readonly Regex FileTypePattern = new(
        $@"\.({string.Join("|", FileTypes.Select(Regex.Escape))})$", 
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(3)
    );
    
    private readonly HttpClient _httpClient;
    
    private readonly ConcurrentDictionary<string, bool> _resourceUrls = [];
    private readonly ConcurrentDictionary<string, bool> _readyToVisitUrls = [];
    private readonly ConcurrentDictionary<string, bool> _errorUrls = [];
    private readonly ConcurrentDictionary<string, bool> _outerUrls = [];

    private readonly string _domain;

    public IReadOnlyCollection<string> ResourceUrls => [.._resourceUrls.Keys];
    public IReadOnlyCollection<string> OuterUrls => [.._outerUrls.Keys];
    public IReadOnlyCollection<string> ErrorUrls => [.._errorUrls.Keys];

    public UrlParser(string resourceUrl, HttpClient httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _readyToVisitUrls.TryAdd(resourceUrl, true);
        _domain = new Uri(resourceUrl).Host;
    }

    public async Task RunAsync(int maxDegreeOfParallelism = 100, CancellationToken cancellationToken = default)
    {
        while (!_readyToVisitUrls.IsEmpty)
        {
            var urlsToVisit = _readyToVisitUrls.ToArray();
            _readyToVisitUrls.Clear();

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };
            await Parallel.ForEachAsync(urlsToVisit, options, async (url, ctx) => await GetUrlAsync(url.Key, ctx));
        }
    }

    private async Task GetUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_resourceUrls.TryAdd(url, true)) return;

            var html = await _httpClient.GetStringAsync(url, cancellationToken);
            ProcessLinks(html);
        }
        catch
        {
            _errorUrls.TryAdd(url, true);
        }
    }
    
    private bool TryGetDomain(string url, out string domain)
    {
        domain = null;
        try
        {
            var uri = new Uri(url);
            domain = uri.Host;
            return true;
        }
        catch
        {
            _errorUrls.TryAdd(url, true);
            return false;
        }
    }
    
    private void ProcessLinks(string html)
    {
        foreach (Match match in HrefPattern.Matches(html))
        {
            var tmpUrl = match.Groups[1].Value;
            
            if (FileTypePattern.IsMatch(tmpUrl)) continue;
            if (!TryGetDomain(tmpUrl, out var tmpDomain)) continue;
            
            if (tmpDomain == _domain)
            {
                _readyToVisitUrls.TryAdd(tmpUrl, true);
            }
            else
            {
                _outerUrls.TryAdd(tmpUrl, true);
            }
        }
    }
}
