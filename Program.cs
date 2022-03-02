using System.Collections.Concurrent;
using System.Text.RegularExpressions;

const string defUrl = "https://mail.ru";
const string hrefPattern = @"href\s*=\s*(?:[""'](?<1>[^""']*)[""']|(?<1>\S+))";

ConcurrentBag<string> visitedUrl = new();
ConcurrentBag<string> errorUrl = new();
ConcurrentBag<string> readyToVisit = new();
HttpClient httpClient = new();

string[] fileType = {".js", ".pdf", ".jpg", ".png", ".gif", ".css", ".jpeg"};

var fileTypePattern = new Regex(string.Join("|", fileType.Select(Regex.Escape)));

Console.Write("Введите URL: ");
var url = Console.ReadLine();
if (url is not null && url.Contains('.') && url.Length >= 3)
{
    readyToVisit.Add(url);
}
else
{
    readyToVisit.Add(defUrl);
}

Console.Write("Введитете глубину обхода от 0 до 2: ");
if (!IsCorrect(Console.ReadLine(), out var deepWalk, 0, 2))
{
    deepWalk = 1;
}

RunParser();

Console.WriteLine($"Ссылок с непрошедшими запросами: {errorUrl.Count}");
Console.WriteLine($"Посещенные ссылки: {visitedUrl.Count}");
Console.WriteLine($"Ссылrи готовые к посещению: {readyToVisit.Count}");

Console.Write("Отобразить полученные ссылки? (y/n): ");

if (Console.ReadLine() == "y")
{
    ShowInfo();
}

bool IsCorrect(string? arg, out int numb, int lf = 0, int rt = int.MaxValue - 1)
{
    if (!int.TryParse(arg, out numb)) return false;
    return numb >= lf && numb <= rt;
}

void RunParser()
{
    // если глубина больше текущей то повторям обход
    while (deepWalk > 0)
    {
        var tempBag = new ConcurrentBag<string>(readyToVisit);
        readyToVisit.Clear();

        Task.WaitAll(tempBag.Select(GetUrlAsync).ToArray());

        tempBag.Clear();

        deepWalk--;
    }
}

async Task GetUrlAsync(string url)
{
    try
    {
        var html = await httpClient.GetStringAsync(url);

        visitedUrl.Add(url);

        // ищем ссылки
        var m = Regex.Match(html, hrefPattern,
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));

        while (m.Success)
        {
            var tmpUrl = $"{m.Groups[1]}";

            // различные ограничения
            if (tmpUrl.IndexOf("http", StringComparison.Ordinal) == 0)
                if (!fileTypePattern.IsMatch(tmpUrl))
                    if (!visitedUrl.Contains(tmpUrl) && !readyToVisit.Contains(tmpUrl))
                        readyToVisit.Add(tmpUrl);

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
    if (errorUrl.IsEmpty)
    {
        Console.WriteLine("errorUrl:");
        foreach (var item in errorUrl)
            Console.WriteLine($"->{item}");
    }

    Console.WriteLine("visitedUrl:");
    foreach (var item in visitedUrl)
        Console.WriteLine($"->{item}");

    Console.WriteLine("readyToVisit:");
    foreach (var item in readyToVisit)
        Console.WriteLine($"->{item}");
}