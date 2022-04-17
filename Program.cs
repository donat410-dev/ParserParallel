using System.Collections.Concurrent;
using System.Text.RegularExpressions;

const string hrefPattern = @"href\s*=\s*(?:[""'](?<1>[^""']*)[""']|(?<1>[^>\s]+))";

ConcurrentBag<string> resourceUrl = new();
ConcurrentBag<string> readyToVisitUrl = new();
ConcurrentBag<string> errorUrl = new();
ConcurrentBag<string> outerUrl = new();
List<Task> tempUrl = new();
HttpClient httpClient = new();
string domain;
string[] fileType = {".js", ".pdf", ".jpg", ".png", ".gif", ".css", ".jpeg", ".xml"};

var fileTypePattern = new Regex(string.Join("|", fileType.Select(Regex.Escape)));

Console.Write("Введите URL: ");
var urlConsole = Console.ReadLine();
if (Uri.IsWellFormedUriString(urlConsole, UriKind.Absolute))
{
    domain = new Regex(@"(?<=\/)[a-z0-9\-\.]+\.[0-9a-z]+(?=\/|$)").Matches(urlConsole)[0].Value;
    domain = domain.Replace("www.", "");
    readyToVisitUrl.Add(urlConsole);
}
else
{
    Console.WriteLine("Вы не ввели URL!");
    Console.ReadKey();
    return;
}

RunParser();

Console.WriteLine($"Страницы ресурса: {resourceUrl.Count}");
Console.WriteLine($"Ссылrи на внешние ресурсы: {outerUrl.Count}");
Console.WriteLine($"Ссылок с непрошедшими запросами: {errorUrl.Count}");

Console.Write("Отобразить полученные ссылки? (y/n): ");

if (Console.ReadLine()!.Contains('y'))
{
    ShowInfo();
    Console.ReadKey();
}

void RunParser()
{
    while (!readyToVisitUrl.IsEmpty)
    {
        while (readyToVisitUrl.TryTake(out var url))
        {
            tempUrl.Add(Task.Run(() => GetUrlAsync(url)));
        }

        Task.WaitAll(tempUrl.ToArray());
        tempUrl.Clear();
    }
}

async Task GetUrlAsync(string url)
{
    try
    {
        var html = await httpClient.GetStringAsync(url);

        resourceUrl.Add(url);
        // ищем ссылки
        var m = Regex.Match(html, hrefPattern,
            RegexOptions.Compiled,
            TimeSpan.FromSeconds(5));

        while (m.Success)
        {
            var tmpUrl = $"{m.Groups[1]}";
            if (tmpUrl[0] == '/') tmpUrl = "https://" + domain + tmpUrl;

            // различные ограничения
            if (Uri.IsWellFormedUriString(tmpUrl, UriKind.Absolute))
            {
                if (!fileTypePattern.IsMatch(tmpUrl))
                {
                    if (!resourceUrl.Contains(tmpUrl) && !readyToVisitUrl.Contains(tmpUrl) &&
                        !outerUrl.Contains(tmpUrl))
                    {
                        if (tmpUrl.Contains(domain)) readyToVisitUrl.Add(tmpUrl);
                        else outerUrl.Add(tmpUrl);
                    }
                }
            }

            m = m.NextMatch();
        }
    }
    catch (Exception e)
    {
        errorUrl.Add(url);
    }
}

void ShowInfo()
{
    Console.WriteLine("Все страницы ресурса:");
    foreach (var item in resourceUrl)
        Console.WriteLine($"->{item}");

    Console.WriteLine("Внешние URL:");
    foreach (var item in outerUrl)
        Console.WriteLine($"->{item}");

    if (errorUrl.IsEmpty) return;
    Console.WriteLine("Непрошедшие запросы:");
    foreach (var item in errorUrl)
        Console.WriteLine($"->{item}");
}