using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParserParallel
{
    public partial class Form1 : Form
    {
        private readonly Stopwatch _stopwatch = new();
        private readonly ConcurrentBag<string> _visitedUrl = new();
        private readonly ConcurrentBag<string> _errorUrl = new();
        private readonly ConcurrentBag<string> _readyToVisit = new();
        private readonly HttpClient _httpClient = new();
        private const string HRefPattern = @"href\s*=\s*(?:[""'](?<1>[^""']*)[""']|(?<1>\S+))";
        private readonly string[] _fileType = { ".js", ".pdf", ".jpg", ".png", ".gif", ".css", ".jpeg" };
        private int _deepWalk;
        private Match _m;
        private Regex _fileTypePattern;
        private ConcurrentBag<string> _tempBag;
        private string _html;

        public Form1()
        {
            InitializeComponent();
        }

        private async void StartButton_Click(object? sender, EventArgs e)
        {
            StartButton.Enabled = false;
            ShowButton.Enabled = false;
            Url.Enabled = false;
            Deep.Enabled = false;

            _visitedUrl.Clear();
            _errorUrl.Clear();
            _readyToVisit.Clear();
            richTextBox1.Clear();

            if (!int.TryParse(Deep.Text, out _deepWalk))
                return;

            _readyToVisit.Add(Url.Text);
            _stopwatch.Restart();
            await Task.Run(RunParser);
            _stopwatch.Stop();
            richTextBox1.AppendText($"Было потрачено {_stopwatch.ElapsedMilliseconds / 1000.0} секунд(ы)\n\n");
            ShowButton.Enabled = true;
            StartButton.Enabled = true;
            Deep.Enabled = true;
            Url.Enabled = true;
            richTextBox1.AppendText($"Ссылок с непрошедшими запросами: {_errorUrl.Count}\n");
            richTextBox1.AppendText($"Посещенные ссылки: {_visitedUrl.Count}\n");
            richTextBox1.AppendText($"Ссылrи готовые к посещению: {_readyToVisit.Count}\n");
        }

        private void RunParser()
        {
            while (true)
            {
                _tempBag = new ConcurrentBag<string>(_readyToVisit);
                _readyToVisit.Clear();

                // устанавливаем число ссылок на данном уровне глубины как максимум для прогрессбара
                Invoke(new Action(() => ProgressBar.Maximum = _tempBag.Count));

                var tasks = new ConcurrentBag<Task>();
                Parallel.ForEach(_tempBag, s => tasks.Add(GetUrlAsync(s)));
                Task.WaitAll(tasks.ToArray());

                _tempBag.Clear();

                // если глубина больше текущей то повторям обход
                if (_deepWalk > 0)
                {
                    _deepWalk--;
                    continue;
                }

                _tempBag.Clear();
                break;
            }
        }

        private async Task GetUrlAsync(string url)
        {
            try
            {
                _html = await _httpClient.GetStringAsync(url);

                _visitedUrl.Add(url);

                // ищем ссылки
                _m = Regex.Match(_html, HRefPattern,
                    RegexOptions.IgnoreCase | RegexOptions.Compiled,
                    TimeSpan.FromSeconds(1));

                while (_m.Success)
                {
                    var tmpUrl = $"{_m.Groups[1]}";

                    // различные ограничения
                    if (tmpUrl.IndexOf("http", StringComparison.Ordinal) == 0)
                        if (!_fileTypePattern.IsMatch(tmpUrl))
                            if (!_visitedUrl.Contains(tmpUrl) && !_readyToVisit.Contains(tmpUrl))
                                _readyToVisit.Add(tmpUrl);

                    _m = _m.NextMatch();
                }
            }
            catch (Exception e)
            {
                _errorUrl.Add(url);
            }
            finally
            {
                Invoke(new Action(() => ProgressBar.PerformStep()));
            }
        }

        private void ShowInfo()
        {
            Invoke(new Action(() =>
            {
                if (_errorUrl.Count > 0)
                {
                    richTextBox1.AppendText("\nerrorUrl:\n");
                    foreach (var item in _errorUrl)
                        richTextBox1.AppendText($"->{item}\n");
                }

                richTextBox1.AppendText("\nvisitedUrl:\n");
                foreach (var item in _visitedUrl)
                    richTextBox1.AppendText($"->{item}\n");

                richTextBox1.AppendText("\nreadyToVisit:\n");
                foreach (var item in _readyToVisit)
                    richTextBox1.AppendText($"->{item}\n");
            }));
        }

        private async void ShowButton_Click(object? sender, EventArgs e)
        {
            StartButton.Enabled = false;
            ShowButton.Enabled = false;
            Url.Enabled = false;
            Deep.Enabled = false;
            await Task.Run(ShowInfo);
            StartButton.Enabled = true;
            ShowButton.Enabled = true;
            Url.Enabled = true;
            Deep.Enabled = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // гененрируем из массива регулярное выражение для исключения ссылок на файлы
            _fileTypePattern = new Regex(string.Join("|", _fileType.Select(Regex.Escape)));
        }
    }
}
