using Markdig;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MemoHub;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private readonly ObservableCollection<MemoRow> _items = new();
    private long? _editingId = null;
    private MemoRow? _currentMemo = null;
    private bool _showActionButtons = true;

    public bool ShowActionButtons
    {
        get => _showActionButtons;
        set
        {
            if (_showActionButtons != value)
            {
                _showActionButtons = value;
                OnPropertyChanged(nameof(ShowActionButtons));
            }
        }
    }

    private readonly MarkdownPipeline _md = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    // 表示モード
    private enum DisplayMode { Empty, View, Edit }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this; // DataContextを設定

        List.ItemsSource = _items;
        LoadList(null);
        
        // WebView2の初期化を待ってから表示
        Loaded += async (s, e) =>
        {
            await ViewPreview.EnsureCoreWebView2Async();
            await EditPreview.EnsureCoreWebView2Async();
            
            // リンククリックで外部ブラウザを開く設定
            ViewPreview.CoreWebView2.NewWindowRequested += (sender, args) =>
            {
                args.Handled = true;
                OpenUri(args.Uri);
            };
            
            EditPreview.CoreWebView2.NewWindowRequested += (sender, args) =>
            {
                args.Handled = true;
                OpenUri(args.Uri);
            };

            // WebView2からのメッセージを受信（クリップボードコピー用）
            ViewPreview.CoreWebView2.WebMessageReceived += (sender, args) =>
            {
                HandleWebMessage(args.WebMessageAsJson);
            };
            
            EditPreview.CoreWebView2.WebMessageReceived += (sender, args) =>
            {
                HandleWebMessage(args.WebMessageAsJson);
            };
            
            // 初期表示：最終10件を読み込み、最初のメモを表示
            LoadListAndSelectFirst(null, 10);
        };
        
        // 保存された設定を読み込み
        LoadSettings();
        
        // 検索ボックスにフォーカス
        SearchBox.Focus();
    }

    private void LoadSettings()
    {
        // ボタン表示設定を読み込み
        var showButtons = Db.GetSetting("ShowActionButtons", "true");
        ShowActionButtons = showButtons == "true";
        
        // トグルボタンの表示を更新（現在の状態ではなく、次のアクションを表示）
        if (ToggleButtonsBtn != null)
        {
            ToggleButtonsBtn.Content = ShowActionButtons ? "👁️‍🗨️ ボタン非表示" : "👁️ ボタン表示";
        }
    }

    private void LoadList(string? q, int? limit = null)
    {
        _items.Clear();
        foreach (var m in Db.List(q, limit))
            _items.Add(m);
    }

    private void LoadListAndSelectFirst(string? q, int? limit = null)
    {
        LoadList(q, limit);
        
        // メモがある場合、最初のメモを選択して表示
        if (_items.Count > 0)
        {
            List.SelectedItem = _items[0];
            ViewMemoMode(_items[0]);
        }
        else
        {
            ShowMode(DisplayMode.Empty);
        }
    }

    // 表示モード切り替え
    private void ShowMode(DisplayMode mode)
    {
        EmptyPanel.Visibility = mode == DisplayMode.Empty ? Visibility.Visible : Visibility.Collapsed;
        ViewPanel.Visibility = mode == DisplayMode.View ? Visibility.Visible : Visibility.Collapsed;
        EditPanel.Visibility = mode == DisplayMode.Edit ? Visibility.Visible : Visibility.Collapsed;
    }

    // WebView2からのメッセージを処理
    private void HandleWebMessage(string jsonMessage)
    {
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(jsonMessage);
            var root = json.RootElement;
            
            if (root.TryGetProperty("action", out var action))
            {
                var actionValue = action.GetString();
                
                if (actionValue == "copy")
                {
                    if (root.TryGetProperty("text", out var text))
                    {
                        var textToCopy = text.GetString();
                        if (!string.IsNullOrEmpty(textToCopy))
                        {
                            Clipboard.SetText(textToCopy);
                        }
                    }
                }
                else if (actionValue == "openLink")
                {
                    if (root.TryGetProperty("url", out var url))
                    {
                        var urlString = url.GetString();
                        if (!string.IsNullOrEmpty(urlString))
                        {
                            OpenUri(urlString);
                        }
                    }
                }
            }
        }
        catch { }
    }

    // URIを適切な方法で開く
    private void OpenUri(string uri)
    {
        try
        {
            // file:/// プロトコルの場合、Windowsパスに変換
            if (uri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                var path = uri.Substring(8); // "file:///" を除去
                path = System.Net.WebUtility.UrlDecode(path); // URLデコード
                path = path.Replace('/', '\\'); // スラッシュをバックスラッシュに変換
                
                // UNCパスの場合（//server/share形式）
                if (path.StartsWith("\\\\"))
                {
                    // 既に正しい形式なので、そのまま開く
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                }
                else
                {
                    // ローカルパスの場合
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
            else
            {
                // HTTP(S)などの通常のURL
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"リンクを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void NewMode()
    {
        _editingId = null;
        _currentMemo = null;
        TitleBox.Text = "";
        ContentBox.Text = "";
        SaveBtn.Content = "💾 保存";
        List.SelectedItem = null;
        ShowMode(DisplayMode.Edit);
        RenderEditPreview();
    }

    private void ViewMemoMode(MemoRow m)
    {
        _currentMemo = m;
        _editingId = null;
        ViewTitle.Text = m.Title;
        ViewDate.Text = $"更新日時: {m.UpdatedAt}";
        ShowMode(DisplayMode.View);
        RenderViewPreview(m.Content);
    }

    private void EditMode(MemoRow m)
    {
        _editingId = m.Id;
        _currentMemo = m;
        TitleBox.Text = m.Title;
        ContentBox.Text = m.Content;
        SaveBtn.Content = "💾 更新";
        ShowMode(DisplayMode.Edit);
        RenderEditPreview();
    }

    private void RenderViewPreview(string content)
    {
        if (ViewPreview.CoreWebView2 == null) return;
        var html = GenerateHtml(content);
        ViewPreview.NavigateToString(html);
    }

    private void RenderEditPreview()
    {
        if (EditPreview.CoreWebView2 == null) return;
        var html = GenerateHtml(ContentBox.Text ?? "");
        EditPreview.NavigateToString(html);
    }

    private string GenerateHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            // 空の場合はプレースホルダーを表示
            return @"
            <html><head>
              <meta charset=""utf-8"" />
              <style>
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
                    padding: 20px;
                    margin: 0;
                    color: #999;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    height: 100vh;
                    font-size: 14px;
                }
              </style>
            </head><body>
            <div>プレビューはここに表示されます</div>
            </body></html>";
        }
        
        var escaped = markdown;
        
        // URL and file paths linkification (before HTML escaping)
        // HTTP(S) URL - includes dots, slashes, query strings, etc.
        escaped = System.Text.RegularExpressions.Regex.Replace(escaped,
            @"(https?://[^\s<>]+)",
            match =>
            {
                var url = match.Groups[1].Value;
                var encodedUrl = System.Net.WebUtility.HtmlEncode(url);
                return $"<a href='{System.Net.WebUtility.HtmlEncode(url)}' style='color: #0969da; text-decoration: underline; cursor: pointer;'>{encodedUrl}</a>";
            });
        
        // UNC path \\server\share\path
        escaped = System.Text.RegularExpressions.Regex.Replace(escaped,
            @"(\\\\[\w\d\.\-]+(?:\\[\w\d\.\-\s]+)+)",
            match =>
            {
                var path = match.Groups[1].Value;
                // UNCパスのfile URLはfile://///server/share/path形式
                var fileUrl = "file:////" + path.Substring(2).Replace("\\", "/"); // \\\\ を除く
                var encodedPath = System.Net.WebUtility.HtmlEncode(path);
                return $"<a href='{System.Net.WebUtility.HtmlEncode(fileUrl)}' style='color: #0969da; text-decoration: underline; cursor: pointer;'>{encodedPath}</a>";
            });
        
        // Windows path C:\path\to\file
        escaped = System.Text.RegularExpressions.Regex.Replace(escaped,
            @"([A-Za-z]:\\(?:[\w\d\.\-\s]+\\)*[\w\d\.\-\s]+)",
            match =>
            {
                var path = match.Groups[1].Value;
                var fileUrl = "file:///" + path.Replace("\\", "/");
                var encodedPath = System.Net.WebUtility.HtmlEncode(path);
                return $"<a href='{System.Net.WebUtility.HtmlEncode(fileUrl)}' style='color: #0969da; text-decoration: underline; cursor: pointer;'>{encodedPath}</a>";
            });
        
        // 残りのテキストをHTMLエスケープ（リンクタグは保持）
        // <a>タグを保護
        var links = new System.Collections.Generic.List<string>();
        escaped = System.Text.RegularExpressions.Regex.Replace(escaped, @"(<a[^>]*>.*?</a>)", match =>
        {
            links.Add(match.Groups[1].Value);
            return $"__LINK_{links.Count - 1}__";
        });
        
        // 通常テキストをエスケープ
        escaped = System.Net.WebUtility.HtmlEncode(escaped);
        
        // リンクを復元
        for (int i = 0; i < links.Count; i++)
        {
            escaped = escaped.Replace($"__LINK_{i}__", links[i]);
        }
        
        return $@"
            <html><head>
              <meta charset=""utf-8"" />
              <style>
                body {{
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
                    padding: 20px;
                    margin: 0;
                }}
                pre {{
                    background: #f6f8fa;
                    padding: 16px;
                    border-radius: 6px;
                    overflow-x: auto;
                    border: 1px solid #e1e4e8;
                    white-space: pre-wrap;
                    word-wrap: break-word;
                    position: relative;
                    font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
                    font-size: 14px;
                    line-height: 1.6;
                    margin: 0;
                }}
                pre:hover .copy-button {{
                    opacity: 1;
                }}
                .copy-button {{
                    position: absolute;
                    top: 8px;
                    right: 8px;
                    background-color: #ffffff;
                    border: 1px solid #d0d7de;
                    border-radius: 6px;
                    padding: 6px 10px;
                    cursor: pointer;
                    opacity: 0;
                    transition: opacity 0.2s, background-color 0.2s;
                    font-size: 12px;
                    color: #24292f;
                }}
                .copy-button:hover {{
                    background-color: #f3f4f6;
                }}
                .copy-button:active {{
                    background-color: #e5e7eb;
                }}
                .copy-button.copied {{
                    background-color: #2da44e;
                    color: white;
                    border-color: #2da44e;
                }}
                a {{
                    color: #0969da;
                    text-decoration: underline;
                    cursor: pointer;
                }}
                a:hover {{
                    text-decoration: none;
                }}
              </style>
              <script>
                var originalMarkdown = `{markdown.Replace("\\", "\\\\").Replace("`", "\\`").Replace("$", "\\$")}`;

                function copyToClipboard(text, button) {{
                  if (window.chrome && window.chrome.webview) {{
                    window.chrome.webview.postMessage({{
                      action: 'copy',
                      text: text
                    }});
                    button.innerHTML = '✓ コピー済み';
                    button.classList.add('copied');
                    setTimeout(function() {{
                      button.innerHTML = '📋 コピー';
                      button.classList.remove('copied');
                    }}, 2000);
                  }}
                }}

                document.addEventListener('DOMContentLoaded', function() {{
                  // リンククリックをインターセプトしてC#に送信
                  document.querySelectorAll('a').forEach(function(link) {{
                    link.addEventListener('click', function(e) {{
                      e.preventDefault();
                      if (window.chrome && window.chrome.webview) {{
                        window.chrome.webview.postMessage({{
                          action: 'openLink',
                          url: link.href
                        }});
                      }}
                    }});
                  }});

                  var pre = document.querySelector('pre');
                  if (pre) {{
                    var button = document.createElement('button');
                    button.className = 'copy-button';
                    button.innerHTML = '📋 コピー';
                    button.addEventListener('click', function() {{
                      copyToClipboard(originalMarkdown, button);
                    }});
                    pre.appendChild(button);
                  }}
                }});
              </script>
            </head><body>
            <pre>{escaped}</pre>
            </body></html>";
    }

    private void Search_Click(object sender, RoutedEventArgs e)
    {
        LoadListAndSelectFirst(SearchBox.Text); // 全件検索して最初のメモを表示
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        // 初期表示に戻る
        SearchBox.Text = "";
        LoadListAndSelectFirst(null, 10);
        SearchBox.Focus();
    }

    private void ToggleButtons_Click(object sender, RoutedEventArgs e)
    {
        ShowActionButtons = !ShowActionButtons;
        if (sender is Button btn)
        {
            btn.Content = ShowActionButtons ? "👁️‍🗨️ ボタン非表示" : "👁️ ボタン表示";
        }
        
        // 設定を保存
        Db.SaveSetting("ShowActionButtons", ShowActionButtons.ToString().ToLower());
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Ctrl+Rでリセット
        if (e.Key == System.Windows.Input.Key.R && 
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            Reset_Click(sender, e);
            e.Handled = true;
        }
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            Search_Click(sender, e);
        }
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        NewMode();
    }

    private void List_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // リスト選択時は閲覧モードで表示
        if (List.SelectedItem is MemoRow m)
            ViewMemoMode(m);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MemoRow m)
        {
            List.SelectedItem = m;
            EditMode(m);
        }
    }

    private void EditCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMemo != null)
            EditMode(_currentMemo);
    }

    private void DeleteCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMemo != null)
            PerformDelete(_currentMemo);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MemoRow m)
            PerformDelete(m);
    }

    private void PerformDelete(MemoRow m)
    {
        var ok = MessageBox.Show(
            $"削除します。\n\n{m.Title}\n\n元に戻せません。よろしいですか？",
            "削除確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (ok != MessageBoxResult.Yes) return;

        Db.Delete(m.Id);
        LoadList(SearchBox.Text);
        ShowMode(DisplayMode.Empty);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMemo != null)
            ViewMemoMode(_currentMemo);
        else
            ShowMode(DisplayMode.Empty);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var title = TitleBox.Text ?? "";
            var content = ContentBox.Text ?? "";

            if (string.IsNullOrWhiteSpace(content))
            {
                MessageBox.Show("内容は必須です。", "入力チェック", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 類似メモをチェック（編集中のメモは除外）
            var similarMemos = FindSimilarMemos(title, content, _editingId);
            
            if (similarMemos.Any())
            {
                var selectedMemo = ShowSimilarMemosDialog(similarMemos, title, content);
                
                if (selectedMemo == null) // キャンセルまたは新規保存
                {
                    // 新規保存として続行
                }
                else
                {
                    // 選択されたメモを上書き
                    Db.Update(selectedMemo.Id, title, content);
                    LoadList(SearchBox.Text);
                    var updatedMemo = _items.FirstOrDefault(x => x.Id == selectedMemo.Id);
                    if (updatedMemo != null)
                    {
                        List.SelectedItem = updatedMemo;
                        ViewMemoMode(updatedMemo);
                        MessageBox.Show("メモを更新しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }
            }

            if (_editingId is null)
            {
                var newId = Db.Insert(title, content);
                LoadList(SearchBox.Text);
                // 新規作成後は作成したメモを表示
                var newMemo = _items.FirstOrDefault(x => x.Id == newId);
                if (newMemo != null)
                {
                    List.SelectedItem = newMemo;
                    ViewMemoMode(newMemo);
                    MessageBox.Show("メモを作成しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                Db.Update(_editingId.Value, title, content);
                LoadList(SearchBox.Text);
                // 更新後は更新したメモを表示
                var updatedMemo = _items.FirstOrDefault(x => x.Id == _editingId.Value);
                if (updatedMemo != null)
                {
                    List.SelectedItem = updatedMemo;
                    ViewMemoMode(updatedMemo);
                    MessageBox.Show("メモを更新しました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // キャンセルされた場合は何もしない
        }
    }

    // 類似メモ選択ダイアログを表示
    private MemoRow? ShowSimilarMemosDialog(List<MemoRow> similarMemos, string newTitle, string newContent)
    {
        var dialog = new Window
        {
            Title = "類似メモ検出",
            Width = 700,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = System.Windows.Media.Brushes.White
        };

        var mainGrid = new Grid { Margin = new Thickness(20) };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ヘッダー
        var header = new TextBlock
        {
            Text = "類似したメモが見つかりました。上書き対象を選択するか、新規保存してください。",
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 15)
        };
        mainGrid.Children.Add(header);

        // 変数を事前に宣言
        MemoRow? selectedMemo = null;
        bool? dialogResult = null;

        // リストボックス
        var listBox = new ListBox
        {
            Margin = new Thickness(0, 0, 0, 15),
            BorderBrush = System.Windows.Media.Brushes.LightGray,
            BorderThickness = new Thickness(1)
        };
        Grid.SetRow(listBox, 1);

        // シングルクリックで上書き
        listBox.MouseUp += (s, e) =>
        {
            if (listBox.SelectedItem is ListBoxItem item && item.Tag is MemoRow memo)
            {
                selectedMemo = memo;
                dialogResult = true;
                dialog.Close();
            }
        };

        foreach (var memo in similarMemos)
        {
            var item = new ListBoxItem
            {
                Tag = memo,
                Padding = new Thickness(10)
            };

            var stackPanel = new StackPanel();
            
            var titleBlock = new TextBlock
            {
                Text = memo.Title,
                FontWeight = FontWeights.Bold,
                FontSize = 14
            };
            stackPanel.Children.Add(titleBlock);

            var dateBlock = new TextBlock
            {
                Text = $"更新日時: {memo.UpdatedAt}",
                FontSize = 11,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 4, 0, 4)
            };
            stackPanel.Children.Add(dateBlock);

            var contentPreview = memo.Content.Length > 150 
                ? memo.Content.Substring(0, 150) + "..." 
                : memo.Content;
            var contentBlock = new TextBlock
            {
                Text = contentPreview,
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.DarkGray,
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(contentBlock);

            item.Content = stackPanel;
            listBox.Items.Add(item);
        }

        mainGrid.Children.Add(listBox);

        // ボタンパネル
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(buttonPanel, 2);

        var overwriteButton = new Button
        {
            Content = "選択したメモを上書き",
            Width = 150,
            Height = 32,
            Margin = new Thickness(0, 0, 10, 0)
        };
        overwriteButton.Click += (s, e) =>
        {
            if (listBox.SelectedItem is ListBoxItem item && item.Tag is MemoRow memo)
            {
                selectedMemo = memo;
                dialogResult = true;
                dialog.Close();
            }
            else
            {
                MessageBox.Show("上書きするメモを選択してください。", "選択エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        buttonPanel.Children.Add(overwriteButton);

        var newSaveButton = new Button
        {
            Content = "新規保存",
            Width = 100,
            Height = 32,
            Margin = new Thickness(0, 0, 10, 0)
        };
        newSaveButton.Click += (s, e) =>
        {
            selectedMemo = null;
            dialogResult = true;
            dialog.Close();
        };
        buttonPanel.Children.Add(newSaveButton);

        var cancelButton = new Button
        {
            Content = "キャンセル",
            Width = 100,
            Height = 32
        };
        cancelButton.Click += (s, e) =>
        {
            dialogResult = false;
            dialog.Close();
        };
        buttonPanel.Children.Add(cancelButton);

        mainGrid.Children.Add(buttonPanel);
        dialog.Content = mainGrid;

        dialog.ShowDialog();

        // キャンセルの場合はnullを返すが、Save_Clickでreturnして処理を中断
        if (dialogResult == false)
        {
            throw new OperationCanceledException(); // キャンセル処理
        }

        return selectedMemo;
    }

    // 類似メモを検索
    private List<MemoRow> FindSimilarMemos(string title, string content, long? excludeId)
    {
        var allMemos = Db.List(null);
        var similarMemos = new List<MemoRow>();

        foreach (var memo in allMemos)
        {
            if (excludeId.HasValue && memo.Id == excludeId.Value)
                continue;

            // タイトルが類似または内容の最初の100文字が類似
            var isSimilarTitle = !string.IsNullOrWhiteSpace(title) && 
                                 !string.IsNullOrWhiteSpace(memo.Title) &&
                                 CalculateSimilarity(title, memo.Title) > 0.6;
            
            var contentPreview = content.Length > 100 ? content.Substring(0, 100) : content;
            var memoPreview = memo.Content.Length > 100 ? memo.Content.Substring(0, 100) : memo.Content;
            var isSimilarContent = CalculateSimilarity(contentPreview, memoPreview) > 0.7;

            if (isSimilarTitle || isSimilarContent)
            {
                similarMemos.Add(memo);
                if (similarMemos.Count >= 3) break; // 最大3件まで
            }
        }

        return similarMemos;
    }

    // 簡易的な類似度計算（Levenshtein距離ベース）
    private double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
            return 0;

        s1 = s1.ToLower();
        s2 = s2.ToLower();

        int maxLen = Math.Max(s1.Length, s2.Length);
        if (maxLen == 0) return 1.0;

        int distance = LevenshteinDistance(s1, s2);
        return 1.0 - (double)distance / maxLen;
    }

    private int LevenshteinDistance(string s1, string s2)
    {
        int[,] d = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            d[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            d[0, j] = j;

        for (int j = 1; j <= s2.Length; j++)
        {
            for (int i = 1; i <= s1.Length; i++)
            {
                int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[s1.Length, s2.Length];
    }

    // メモをクリップボードにコピー
    private void CopyMemoToClipboard(MemoRow memo)
    {
        try
        {
            // MarkdownをHTMLに変換してから、HTMLタグを除去してプレーンテキストに
            var html = Markdown.ToHtml(memo.Content, _md);
            var plainText = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
            // HTML entities をデコード
            plainText = System.Net.WebUtility.HtmlDecode(plainText);
            Clipboard.SetText(plainText);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"コピーに失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MemoRow m)
            CopyMemoToClipboard(m);
    }

    private void CopyCurrent_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMemo != null)
            CopyMemoToClipboard(_currentMemo);
    }

    private void ContentBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RenderEditPreview();
    }
}
