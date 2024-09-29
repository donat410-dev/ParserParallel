using ParserParallel;

Console.Write("Введите URL: ");
var urlConsole = Console.ReadLine();

while (!Uri.IsWellFormedUriString(urlConsole, UriKind.Absolute))
{
    Console.Write("Введите корректный URL: ");
    urlConsole = Console.ReadLine();
}
var parser = new UrlParser(urlConsole);

await parser.RunAsync();

var resourceUrl = parser.ResourceUrls;
var errorUrl = parser.ErrorUrls;
var outerUrl = parser.OuterUrls;

Console.WriteLine($"Страницы ресурса: {resourceUrl.Count}");
Console.WriteLine($"Ссылки на внешние ресурсы: {outerUrl.Count}");
Console.WriteLine($"Ссылок с непрошедшими запросами: {errorUrl.Count}");

Console.Write("Отобразить полученные ссылки? (y/n): ");

if (Console.ReadLine()!.Contains('y'))
{
    Console.WriteLine("Все страницы ресурса:");
    foreach (var item in resourceUrl) Console.WriteLine($"->{item}");
    
    Console.WriteLine("Внешние URL:");
    foreach (var item in outerUrl) Console.WriteLine($"->{item}");
    
    Console.WriteLine("Непрошедшие запросы:");
    foreach (var item in errorUrl) Console.WriteLine($"->{item}");
}

Console.ReadKey();