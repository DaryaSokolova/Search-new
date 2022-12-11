using System;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using System.Diagnostics;
using LuceneDirectory = Lucene.Net.Store.Directory;
using System.IO;
using System.Linq;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Analysis.Ru;
using Lucene.Net.Analysis.En;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Search.Spell;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Search
{
    class Program
    {
        const LuceneVersion luceneVersion = LuceneVersion.LUCENE_48;

        static IndexWriter writer;
        static Analyzer analyzer;

        static void Search(string toSearch)
        {
            string indexName = "news_index";
            string indexPath = Path.Combine(Environment.CurrentDirectory, indexName);

            using (LuceneDirectory indexDir = FSDirectory.Open(indexPath))
            {
                analyzer = new RussianAnalyzer(luceneVersion);

                IndexWriterConfig indexConfig = new IndexWriterConfig(luceneVersion, analyzer);
                indexConfig.OpenMode = OpenMode.CREATE;                           
                writer = new IndexWriter(indexDir, indexConfig);
                var filesIndexed = 0;
                foreach (string file in System.IO.Directory.EnumerateFiles("C://scrap", "*.txt"))
                {
                    try
                    {
                        Document doc = new Document();
                        string[] contents = File.ReadAllLines(file, System.Text.Encoding.GetEncoding("windows-1251"));
                        doc.Add(new StoredField("filename", file));
                        doc.Add(new TextField("title", contents[0], Field.Store.YES));
                        doc.Add(new TextField("content", contents[1], Field.Store.YES));
                        writer.AddDocument(doc);
                        filesIndexed++;
                    }
                    catch (IndexOutOfRangeException) { }
                    if (filesIndexed >= int.MaxValue)
                    {
                        break;
                    }
                }

                string searchText = toSearch.ToLower();
                using (DirectoryReader reader = writer.GetReader(applyAllDeletes: true))
                {
                    SpellChecker spellChecker = new SpellChecker(new RAMDirectory());
                    IndexWriterConfig config = new IndexWriterConfig(luceneVersion, analyzer);
                    spellChecker.IndexDictionary(new LuceneDictionary(reader, "content"), config, fullMerge: false);

                    string[] suggestions = spellChecker.SuggestSimilar(searchText, 2);

                    IndexSearcher searcher = new IndexSearcher(reader);
                    QueryParser parser = new MultiFieldQueryParser(luceneVersion, new string[] { "title", "content" }, analyzer);

                    var searchTerms = new string[] { searchText }.Union(suggestions);
                    Console.WriteLine("Searching: " + string.Join(", ", searchTerms));
                    IEnumerable<ScoreDoc> foundDocs = new ScoreDoc[] { };

                    foreach (var searchTerm in searchTerms)
                    {
                        Query query = parser.Parse(searchTerm);
                        TopDocs topDocs = searcher.Search(query, n: int.MaxValue);

                        Console.WriteLine($"Matching results: {topDocs.TotalHits}");
                        foundDocs = topDocs.ScoreDocs.Take(int.MaxValue).Union(foundDocs);
                    }
                    foundDocs = foundDocs.OrderByDescending(x => x.Score).Take(int.MaxValue).ToArray();

                    List<string> resD = new List<string>();

                    foreach (var doc in foundDocs)
                    {
                        Document resultDoc = searcher.Doc(doc.Doc);
                        string domainFull = resultDoc.Get("filename");
                        string[] domainSplit = new Regex(@"//").Split(domainFull);
                        string domain = domainSplit[domainSplit.Length - 1];
                        resD.Add(domain);
                    }
                    resD = resD.Distinct().ToList();

                    for (int i = 0; i < resD.Count && i < 10; i++)
                    {
                        Console.WriteLine($"{i + 1}: {resD[i]}");
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            Search("Автобус");
            Console.ReadKey();
        }
    }
}