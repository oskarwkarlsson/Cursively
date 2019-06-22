﻿using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace Cursively.Inputs
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class CsvMemoryMappedFileInput : CsvInput
    {
        private readonly string _csvFilePath;

        private readonly bool _ignoreUTF8ByteOrderMark;

        internal CsvMemoryMappedFileInput(byte delimiter, string csvFilePath, bool ignoreUTF8ByteOrderMark)
            : base(delimiter, requiresExplicitReset: false)
        {
            _csvFilePath = csvFilePath;
            _ignoreUTF8ByteOrderMark = ignoreUTF8ByteOrderMark;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delimiter"></param>
        /// <returns></returns>
        public CsvMemoryMappedFileInput WithDelimiter(byte delimiter) =>
            new CsvMemoryMappedFileInput(delimiter, _csvFilePath, _ignoreUTF8ByteOrderMark);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ignoreUTF8ByteOrderMark"></param>
        /// <returns></returns>
        public CsvMemoryMappedFileInput WithIgnoreUTF8ByteOrderMark(bool ignoreUTF8ByteOrderMark) =>
            new CsvMemoryMappedFileInput(Delimiter, _csvFilePath, ignoreUTF8ByteOrderMark);

        /// <inheritdoc />
        protected override unsafe void Process(CsvTokenizer tokenizer, CsvReaderVisitorBase visitor)
        {
            using (var fl = new FileStream(_csvFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
            {
                long length = fl.Length;
                if (length == 0)
                {
                    return;
                }

                using (var memoryMappedFile = MemoryMappedFile.CreateFromFile(fl, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true))
                using (var accessor = memoryMappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
                {
                    var handle = accessor.SafeMemoryMappedViewHandle;
                    byte* ptr = null;
                    RuntimeHelpers.PrepareConstrainedRegions();
                    try
                    {
                        handle.AcquirePointer(ref ptr);

                        if (_ignoreUTF8ByteOrderMark &&
                            length >= 3 &&
                            ptr[0] == 0xEF &&
                            ptr[1] == 0xBB &&
                            ptr[2] == 0xBF)
                        {
                            length -= 3;
                            ptr += 3;
                        }

                        while (length > int.MaxValue)
                        {
                            tokenizer.ProcessNextChunk(new ReadOnlySpan<byte>(ptr, int.MaxValue), visitor);
                            length -= int.MaxValue;
                            ptr += int.MaxValue;
                        }

                        tokenizer.ProcessNextChunk(new ReadOnlySpan<byte>(ptr, unchecked((int)length)), visitor);
                        tokenizer.ProcessEndOfStream(visitor);
                    }
                    finally
                    {
                        if (ptr != null)
                        {
                            handle.ReleasePointer();
                        }
                    }
                }
            }
        }
    }
}
