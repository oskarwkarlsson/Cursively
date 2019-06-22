﻿using System;
using System.Buffers;
using System.IO;

using Cursively.Inputs;

namespace Cursively
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class CsvInput
    {
        private readonly bool _mustResetAfterProcessing;

        private bool _alreadyProcessed;

        /// <summary>
        /// 
        /// </summary>
        protected CsvInput()
            : this((byte)',', true)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delimiter"></param>
        /// <param name="mustResetAfterProcessing"></param>
        protected CsvInput(byte delimiter, bool mustResetAfterProcessing)
        {
            _mustResetAfterProcessing = mustResetAfterProcessing;
            Delimiter = delimiter;
        }

        /// <summary>
        /// 
        /// </summary>
        protected byte Delimiter { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="csvStream"></param>
        /// <returns></returns>
        public static CsvStreamInput ForStream(Stream csvStream)
        {
            return ForStream(csvStream, 81920);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="csvStream"></param>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public static CsvStreamInput ForStream(Stream csvStream, int bufferSize)
        {
            if (csvStream is null)
            {
                throw new ArgumentNullException(nameof(csvStream));
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize, "Must be greater than zero.");
            }

            if (!csvStream.CanRead)
            {
                throw new ArgumentException("Stream does not support reading.", nameof(csvStream));
            }

            return new CsvStreamInput((byte)',', csvStream, bufferSize, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="csvFilePath"></param>
        /// <returns></returns>
        public static CsvMemoryMappedFileInput ForFile(string csvFilePath)
        {
            return new CsvMemoryMappedFileInput((byte)',', csvFilePath, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static CsvBytesInput ForBytes(ReadOnlyMemory<byte> bytes)
        {
            return new CsvBytesInput((byte)',', bytes, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static CsvCharsInput ForString(string str)
        {
            return ForChars(str.AsMemory());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="str"></param>
        /// <param name="encodeBatchCharCount"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public static CsvCharsInput ForString(string str, int encodeBatchCharCount)
        {
            return ForChars(str.AsMemory(), encodeBatchCharCount);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chars"></param>
        /// <returns></returns>
        public static CsvCharsInput ForChars(ReadOnlyMemory<char> chars)
        {
            return ForChars(chars, 340);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chars"></param>
        /// <param name="encodeBatchCharCount"></param>
        /// <returns></returns>
        public static CsvCharsInput ForChars(ReadOnlyMemory<char> chars, int encodeBatchCharCount)
        {
            if (encodeBatchCharCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(encodeBatchCharCount), encodeBatchCharCount, "Must be greater than zero.");
            }

            return new CsvCharsInput((byte)',', chars, encodeBatchCharCount, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chars"></param>
        /// <returns></returns>
        public static CsvCharSequenceInput ForChars(ReadOnlySequence<char> chars)
        {
            return ForChars(chars, 340);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chars"></param>
        /// <param name="encodeBatchCharCount"></param>
        /// <returns></returns>
        public static CsvCharSequenceInput ForChars(ReadOnlySequence<char> chars, int encodeBatchCharCount)
        {
            if (encodeBatchCharCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(encodeBatchCharCount), encodeBatchCharCount, "Must be greater than zero.");
            }

            return new CsvCharSequenceInput((byte)',', chars, encodeBatchCharCount, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textReader"></param>
        /// <returns></returns>
        public static CsvTextReaderInput ForTextReader(TextReader textReader)
        {
            return ForTextReader(textReader, 1024);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textReader"></param>
        /// <param name="readBufferCharCount"></param>
        /// <returns></returns>
        public static CsvTextReaderInput ForTextReader(TextReader textReader, int readBufferCharCount)
        {
            return ForTextReader(textReader, readBufferCharCount, 340);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textReader"></param>
        /// <param name="readBufferCharCount"></param>
        /// <param name="encodeBatchCharCount"></param>
        /// <returns></returns>
        public static CsvTextReaderInput ForTextReader(TextReader textReader, int readBufferCharCount, int encodeBatchCharCount)
        {
            if (readBufferCharCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(readBufferCharCount), readBufferCharCount, "Must be greater than zero.");
            }

            if (encodeBatchCharCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(encodeBatchCharCount), encodeBatchCharCount, "Must be greater than zero.");
            }

            return new CsvTextReaderInput((byte)',', textReader ?? TextReader.Null, readBufferCharCount, encodeBatchCharCount, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="visitor"></param>
        public void Process(CsvReaderVisitorBase visitor)
        {
            if (_alreadyProcessed && _mustResetAfterProcessing)
            {
                throw new InvalidOperationException("Input was already processed.  Call TryReset() first to try to reset this input.  If that method returns false, then this input will not work.");
            }

            Process(new CsvTokenizer(Delimiter), visitor);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool TryReset()
        {
            if (_alreadyProcessed)
            {
                if (!TryResetCore())
                {
                    return false;
                }

                _alreadyProcessed = false;
            }

            return !_alreadyProcessed;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tokenizer"></param>
        /// <param name="visitor"></param>
        protected abstract void Process(CsvTokenizer tokenizer, CsvReaderVisitorBase visitor);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual bool TryResetCore() => true;
    }
}
