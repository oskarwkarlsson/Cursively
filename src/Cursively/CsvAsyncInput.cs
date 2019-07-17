﻿using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

using Cursively.Inputs;

namespace Cursively
{
    /// <summary>
    /// Helpers to create inputs that describe CSV data streams asynchronously.
    /// </summary>
    public static class CsvAsyncInput
    {
        /// <summary>
        /// Creates an input that can describe the contents of a given <see cref="Stream"/> to an
        /// instance of <see cref="CsvReaderVisitorBase"/>, asynchronously.
        /// </summary>
        /// <param name="csvStream">
        /// The <see cref="Stream"/> that contains the CSV data.
        /// </param>
        /// <returns>
        /// An instance of <see cref="CsvAsyncStreamInput"/> wrapping <paramref name="csvStream"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="csvStream"/> is non-<see langword="null"/> and its
        /// <see cref="Stream.CanRead"/> is <see langword="false"/>.
        /// </exception>
        [SuppressMessage("Microsoft.Design", "CA1062:ValidateArgumentsOfPublicMethods")] // Microsoft.CodeAnalysis.FxCopAnalyzers 2.9.3 has a false positive.  Remove when fixed
        public static CsvAsyncStreamInput ForStream(Stream csvStream)
        {
            csvStream = csvStream ?? Stream.Null;
            if (!csvStream.CanRead)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new ArgumentException("Stream does not support reading.", nameof(csvStream));
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            return new CsvAsyncStreamInput((byte)',', csvStream, 65536, ArrayPool<byte>.Shared, true);
        }

        /// <summary>
        /// Creates an input that can describe the contents of a given <see cref="PipeReader"/> to
        /// an instance of <see cref="CsvReaderVisitorBase"/>, asynchronously.
        /// </summary>
        /// <param name="reader">
        /// The <see cref="PipeReader"/> that contains the CSV data.
        /// </param>
        /// <returns>
        /// An instance of <see cref="CsvPipeReaderInput"/> wrapping <paramref name="reader"/>.
        /// </returns>
        public static CsvPipeReaderInput ForPipeReader(PipeReader reader)
        {
            reader = reader ?? NullPipeReader.Instance;
            return new CsvPipeReaderInput((byte)',', reader, true);
        }

        private sealed class NullPipeReader : PipeReader
        {
            public static readonly NullPipeReader Instance = new NullPipeReader();

            private NullPipeReader() { }

            public override void AdvanceTo(SequencePosition consumed) { }

            public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) { }

            public override void CancelPendingRead() { }

            public override void Complete(Exception exception = null) { }

            public override void OnWriterCompleted(Action<Exception, object> callback, object state) { }

            public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
            {
                TryRead(out var result);
                return new ValueTask<ReadResult>(result);
            }

            public override bool TryRead(out ReadResult result)
            {
                result = new ReadResult(ReadOnlySequence<byte>.Empty, false, true);
                return true;
            }
        }
    }
}