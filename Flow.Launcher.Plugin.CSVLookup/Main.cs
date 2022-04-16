using System;
using System.Collections.Generic;
using Flow.Launcher.Plugin;
using MiniExcelLibs;
using System.Linq;
using System.IO;


#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
//TODO: Rename "row" to "cell"

namespace Flow.Launcher.Plugin.CSVLookup
{
    public class CSVLookup : IPlugin
    {
        private PluginInitContext _context;
        private string folder = "C:\\Users\\andre\\Desktop\\Stuff";
        public List<ColumnHeader> cache = new List<ColumnHeader>();

        public void Init(PluginInitContext context)
        {
            _context = context; 
            ImportFiles();
        }

        public List<Result> Query(Query query)
        {
            //Special case: Empty query (with action word)
            if (query.Search.Trim() == "")
                return cache.Select(i => i.ToResult(_context)).ToList();

            if (query.Search.Trim() == "debug")
                System.Diagnostics.Debugger.Launch();

            List<Result> results = new List<Result>();

            //Exact column match (with no fuzzy search)
            string q = query.Search.TrimEnd();
            string[] q_parts = q.Split('=', 2, StringSplitOptions.TrimEntries);
            string q1 = q_parts[0].ToLower(); 
            string q2 = q_parts.Length == 2 ? q_parts[1] : "";
            if (cache.Find(i => q1.Equals(i.Header)) is ColumnHeader c)
            {
                if (q2.EndsWith(';'))
                {
                    //Search!
                    q2 = q2.Substring(0, q2.Length - 1);
                    if (c.Columns.SelectMany(i => i.GetRows()).FirstOrDefault(i => i.Value.Trim().Equals(q2)) is Row row)
                    {
                        results.AddRange(row.AssociatedValues(_context));
                    }
                }
                else if (q2 == "")
                {
                    //List out the rows
                    results.AddRange(c.Columns.SelectMany(i => i.GetRows()).Select(i => i.ToResult(_context)));
                }
                else
                {
                    //Fuzzy search rows
                    foreach (Row row in c.Columns.SelectMany(i => i.GetRows()))
                    {
                        var fuzzyResult = _context.API.FuzzySearch(q2, row.Value);
                        if (fuzzyResult.IsSearchPrecisionScoreMet())
                        {
                            Result result = row.ToResult(_context);
                            result.Score = fuzzyResult.Score;
                            results.Add(result);
                        }
                    }
                }
            }

            //Fuzzy search rows

            //Fuzzy search columns
            foreach (ColumnHeader columnHeader in cache)
            {
                var fuzzyResult = _context.API.FuzzySearch(query.Search, columnHeader.Header);
                if (fuzzyResult.IsSearchPrecisionScoreMet())
                {
                    Result result = columnHeader.ToResult(_context);
                    result.Score = fuzzyResult.Score;
                    results.Add(result);
                }
            }

            return results;
        }

        private void ImportFiles()
        {
            var files = System.IO.Directory.GetFiles(folder);
            foreach (var file in files)
            {
                if (!(file.EndsWith(".csv") || file.EndsWith(".xls") || file.EndsWith(".xlsx")))
                    continue;

                try {
                    var _columns = MiniExcel.GetColumns(file, useHeaderRow: true);
                    for (int i = 0; i < _columns.Count; i++)
                    {
                        var column = new Column(
                            header: _columns.ElementAt(i),
                            filePath: file,
                            index: i
                        );
                        if (cache.Find(j => j.Header == column.Header) is ColumnHeader existingHeader)
                        {
                            existingHeader.AddColumn(column);
                        }
                        else
                        {
                            var columnHeader = new ColumnHeader(column.Header);
                            columnHeader.AddColumn(column);
                            cache.Add(columnHeader);
                        }
                    }
                } catch {}
            }
        }

    }

    public class ColumnHeader
    {
        public string Header { get; set; }
        private List<Column> _columns = new List<Column>();
        public List<Column> Columns { get { return _columns; } }
        public HashSet<string> referencedFilenames = new HashSet<string>();

        public ColumnHeader(string header)
        {
            Header = header.ToLower();
        }

        public void AddColumn(Column column)
        {
            _columns.Add(column);
            referencedFilenames.Add(Path.GetFileNameWithoutExtension(column.FilePath));
        }

        public Result ToResult(PluginInitContext context)
        {
            string subTitle = "";
            foreach (var filename in referencedFilenames)
            {
                if (subTitle == "")
                    subTitle = "Found in " + filename;
                else
                    subTitle += ", " + filename;
            }
            return new Result()
            {
                Title = this.Header,
                SubTitle = subTitle,
                Action = e =>
                {
                    context.API.ChangeQuery(context.CurrentPluginMetadata.ActionKeyword + " " + this.Header + "=");
                    return false;
                }
            };
        }
    }
    
    public class Column
    {
        public string Header { get; set; }
        public string FilePath { get; set; }
        public int Index { get; set; }

        public Column(string header, string filePath, int index)
        {
            Header = header.Trim().ToLower();
            FilePath = filePath;
            Index = index;
        }

        private List<Row> _rows = null;

        public List<Row> GetRows()
        {
            if (_rows != null)
                return _rows;

            IEnumerable<dynamic> data = MiniExcel.Query(path: FilePath, useHeaderRow: true);
            if (data.Count() == 0)
            {
                _rows = new List<Row>();
                return _rows;
            }

            //This code is much more compliated then it should be.
            //The "proper way" assumes the keys are unique (which they might not be)
            _rows = new List<Row>();
            int rowIndex = -1;
            foreach (IDictionary<string, object> row in data)
            {
                rowIndex++;
                if (row.Values.ElementAtOrDefault(Index) is string value)
                    _rows.Add(new Row(value, Header, FilePath, rowIndex));
            }
            return _rows;
        }
    }

    public class Row
    {
        public string Value { get; set; }
        public string Header { get; set; }
        public string FilePath { get; set; }
        public string Filename { get; set; }
        public int Index { get; set; }

        public Row(string value, string header, string filePath, int index)
        {
            Value = value;
            Header = header;
            FilePath = filePath;
            Filename = Path.GetFileNameWithoutExtension(FilePath);
            Index = index;

        }

        public List<Result> AssociatedValues(PluginInitContext context)
        {
            List<Result> result = new List<Result>();
            IEnumerable<dynamic> data = MiniExcel.Query(path: FilePath, useHeaderRow: true);
             
            if (data.ElementAtOrDefault(Index) is IDictionary<string, object> row)
            {
                foreach (var item in row)
                {
                    if (item.Value == null || item.Key.ToLower().Trim() == Header.ToLower().Trim())
                        continue;

                    result.Add(new Result()
                    {
                        SubTitle = item.Key + " - Press enter to copy",
                        Title = item.Value.ToString(),
                        Action = e =>
                        {
                            context.API.CopyToClipboard(item.Value.ToString());
                            context.API.ShowMsg("Copied", "Value copied to clipboard");
                            return true;
                        }
                    });
                }
            }
            return result;
        }

        public Result ToResult(PluginInitContext context)
        {
            return new Result()
            {
                Title = this.Value,
                SubTitle = "From " + this.Filename,
                Action = e =>
                {
                    context.API.ChangeQuery(context.CurrentPluginMetadata.ActionKeyword + " " + this.Header + "=" + this.Value + ";");
                    return false;
                }
            };
        }
    }
}