using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace MemoHub;

public static class Db
{
    public static string GetDbPath()
    {
        var exeDir = AppContext.BaseDirectory;          // exeのあるフォルダ
        var dataDir = Path.Combine(exeDir, "data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "memohub.db");
    }

    public static SqliteConnection Open()
    {
        var path = GetDbPath();
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        Init(conn);
        return conn;
    }

    private static void Init(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS memos (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          title TEXT NOT NULL,
          content TEXT NOT NULL,
          updated_at TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_memos_title ON memos(title);
        
        CREATE TABLE IF NOT EXISTS settings (
          key TEXT PRIMARY KEY,
          value TEXT NOT NULL
        );
        """;
        cmd.ExecuteNonQuery();
    }

    // 設定を保存
    public static void SaveSetting(string key, string value)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        INSERT INTO settings(key, value) VALUES ($key, $value)
        ON CONFLICT(key) DO UPDATE SET value = $value;
        """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    // 設定を読み込み
    public static string? GetSetting(string key, string? defaultValue = null)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? defaultValue;
    }

    public static List<MemoRow> List(string? q, int? limit = null)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();

        var limitValue = limit ?? 300; // デフォルトは300件

        if (!string.IsNullOrWhiteSpace(q))
        {
            cmd.CommandText = $@"
            SELECT id, title, content, updated_at
            FROM memos
            WHERE title LIKE $like OR content LIKE $like
            ORDER BY updated_at DESC
            LIMIT {limitValue};
            ";
            cmd.Parameters.AddWithValue("$like", $"%{q.Trim()}%");
        }
        else
        {
            cmd.CommandText = $@"
            SELECT id, title, content, updated_at
            FROM memos
            ORDER BY updated_at DESC
            LIMIT {limitValue};
            ";
        }

        var list = new List<MemoRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new MemoRow
            {
                Id = r.GetInt64(0),
                Title = r.GetString(1),
                Content = r.GetString(2),
                UpdatedAt = r.GetString(3),
            });
        }
        return list;
    }

    public static long Insert(string title, string content)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        INSERT INTO memos(title, content, updated_at)
        VALUES ($title, $content, $updated);
        """;
        cmd.Parameters.AddWithValue("$title", string.IsNullOrWhiteSpace(title) ? "memo" : title.Trim());
        cmd.Parameters.AddWithValue("$content", content.Trim());
        cmd.Parameters.AddWithValue("$updated", Now());
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT last_insert_rowid();";
        return (long)cmd.ExecuteScalar()!;
    }

    public static void Update(long id, string title, string content)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        UPDATE memos
        SET title=$title, content=$content, updated_at=$updated
        WHERE id=$id;
        """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$title", string.IsNullOrWhiteSpace(title) ? "memo" : title.Trim());
        cmd.Parameters.AddWithValue("$content", content.Trim());
        cmd.Parameters.AddWithValue("$updated", Now());
        cmd.ExecuteNonQuery();
    }

    public static void Delete(long id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM memos WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

public sealed class MemoRow
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    
    // Markdown記号を除去したプレーンテキスト
    public string ContentPreview
    {
        get
        {
            if (string.IsNullOrEmpty(Content)) return "";
            
            // 簡易的なMarkdown記号の除去
            var text = Content;
            
            // コードブロック ``` を除去
            text = System.Text.RegularExpressions.Regex.Replace(text, @"```[\s\S]*?```", "");
            // インラインコード ` を除去
            text = System.Text.RegularExpressions.Regex.Replace(text, @"`([^`]+)`", "$1");
            // 見出し # を除去
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^#{1,6}\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            // 太字 ** を除去
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*([^\*]+)\*\*", "$1");
            // イタリック * を除去
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\*([^\*]+)\*", "$1");
            // リンク [text](url) を text のみに
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+)\]\([^\)]+\)", "$1");
            // リスト - を除去
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^[\-\*\+]\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            // 番号付きリスト 1. を除去
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^\d+\.\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            // 引用 > を除去
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^>\s+", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            
            return text.Trim();
        }
    }
}
