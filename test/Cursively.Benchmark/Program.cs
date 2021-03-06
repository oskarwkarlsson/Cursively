﻿using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using CsvHelper;
using CsvHelper.Configuration;

namespace Cursively.Benchmark
{
    [GcServer(true)]
    [MemoryDiagnoser]
    public class Program
    {
        public static CsvFile[] CsvFiles => GetCsvFiles();

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(CsvFiles))]
        public long CountRowsUsingCursivelyRaw(CsvFile csvFile)
        {
            var visitor = new RowCountingVisitor();
            var tokenizer = new CsvTokenizer();
            tokenizer.ProcessNextChunk(csvFile.FileData, visitor);
            tokenizer.ProcessEndOfStream(visitor);
            return visitor.RowCount;
        }

        [Benchmark]
        [ArgumentsSource(nameof(CsvFiles))]
        public long CountRowsUsingCursivelyArrayInput(CsvFile csvFile)
        {
            var visitor = new RowCountingVisitor();
            CsvSyncInput.ForMemory(csvFile.FileData).Process(visitor);
            return visitor.RowCount;
        }

        [Benchmark]
        [ArgumentsSource(nameof(CsvFiles))]
        public long CountRowsUsingCursivelyMemoryMappedFileInput(CsvFile csvFile)
        {
            var visitor = new RowCountingVisitor();
            CsvSyncInput.ForMemoryMappedFile(csvFile.FullPath).Process(visitor);
            return visitor.RowCount;
        }

        [Benchmark]
        [ArgumentsSource(nameof(CsvFiles))]
        public long CountRowsUsingCursivelyFileStreamInput(CsvFile csvFile)
        {
            var visitor = new RowCountingVisitor();
            using (var stream = new FileStream(csvFile.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
            {
                CsvSyncInput.ForStream(stream).Process(visitor);
            }

            return visitor.RowCount;
        }

        [Benchmark]
        [ArgumentsSource(nameof(CsvFiles))]
        public async Task<long> CountRowsUsingCursivelyAsyncFileStreamInput(CsvFile csvFile)
        {
            var visitor = new RowCountingVisitor();
            using (var stream = new FileStream(csvFile.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await CsvAsyncInput.ForStream(stream).ProcessAsync(visitor);
            }

            return visitor.RowCount;
        }

        [Benchmark]
        [ArgumentsSource(nameof(CsvFiles))]
        public long CountRowsUsingCsvHelper(CsvFile csvFile)
        {
            using (var ms = new MemoryStream(csvFile.FileData, false))
            using (var tr = new StreamReader(ms, new UTF8Encoding(false, true), false))
            using (var rd = new CsvReader(tr, new Configuration { BadDataFound = null }))
            {
                long cnt = 0;
                while (rd.Read())
                {
                    ++cnt;
                }

                return cnt;
            }
        }

        private static async Task<int> Main()
        {
            var prog = new Program();
            foreach (var csvFile in CsvFiles)
            {
                long rowCount = prog.CountRowsUsingCursivelyRaw(csvFile);
                if (prog.CountRowsUsingCsvHelper(csvFile) != rowCount ||
                    prog.CountRowsUsingCursivelyArrayInput(csvFile) != rowCount ||
                    prog.CountRowsUsingCursivelyMemoryMappedFileInput(csvFile) != rowCount ||
                    prog.CountRowsUsingCursivelyFileStreamInput(csvFile) != rowCount ||
                    await prog.CountRowsUsingCursivelyAsyncFileStreamInput(csvFile) != rowCount)
                {
                    Console.Error.WriteLine($"Failed on {csvFile}.");
                    return 1;
                }
            }

            BenchmarkRunner.Run<Program>();
            return 0;
        }

        public readonly struct CsvFile
        {
            public CsvFile(string fullPath) =>
                (FullPath, FileName, FileData) = (fullPath, Path.GetFileNameWithoutExtension(fullPath), File.ReadAllBytes(fullPath));

            public string FullPath { get; }

            public string FileName { get; }

            public byte[] FileData { get; }

            public override string ToString() => FileName;
        }

        private static CsvFile[] GetCsvFiles([CallerFilePath]string myLocation = null)
        {
            string csvFileDirectoryPath = Path.Combine(Path.GetDirectoryName(myLocation), "large-csv-files");
            if (!Directory.Exists(csvFileDirectoryPath))
            {
                string tmpDirectoryPath = csvFileDirectoryPath + "-tmp";
                if (Directory.Exists(tmpDirectoryPath))
                {
                    Directory.Delete(tmpDirectoryPath, true);
                }

                string zipFilePath = csvFileDirectoryPath + ".zip";
                Directory.CreateDirectory(tmpDirectoryPath);
                ZipFile.ExtractToDirectory(zipFilePath, tmpDirectoryPath);
                Directory.Move(tmpDirectoryPath, csvFileDirectoryPath);
            }

            return Array.ConvertAll(Directory.GetFiles(csvFileDirectoryPath, "*.csv"),
                                    fullPath => new CsvFile(fullPath));
        }

        private sealed class RowCountingVisitor : CsvReaderVisitorBase
        {
            public long RowCount { get; private set; }

            public override void VisitEndOfRecord() => ++RowCount;

            public override void VisitEndOfField(ReadOnlySpan<byte> chunk) { }

            public override void VisitPartialFieldContents(ReadOnlySpan<byte> chunk) { }
        }
    }
}
