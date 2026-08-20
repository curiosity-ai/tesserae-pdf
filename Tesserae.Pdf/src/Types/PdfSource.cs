using System;
using System.Collections.Generic;
using Transpose;
using Transpose.Core;
using static Transpose.Core.dom;

namespace Tesserae.Pdf
{
    /// <summary>
    /// Where a document comes from, and how it should be fetched - a URL or a block of bytes, plus
    /// the password, headers and range-request behaviour that go with it.
    ///
    /// This is the type every component and every headless call takes, so a document opened in a
    /// viewer and one opened for text extraction are described the same way. Build one with
    /// <see cref="FromUrl"/> or <see cref="FromBytes(byte[])"/> and add to it fluently:
    ///
    /// <code>
    /// PdfSource.FromUrl("/api/files/42.pdf").WithCredentials().WithRangeChunkSize(512 * 1024)
    /// </code>
    ///
    /// A plain string works anywhere a source is wanted - there is an implicit conversion - so
    /// <c>viewer.Url("report.pdf")</c> and <c>viewer.Source(PdfSource.FromUrl("report.pdf"))</c> are
    /// the same thing.
    /// </summary>
    public sealed class PdfSource
    {
        private readonly string           _url;
        private readonly es5.Uint8Array   _data;
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();

        private string       _password;
        private bool         _withCredentials;
        private int          _rangeChunkSize;
        private string       _docBaseUrl;
        private bool         _disableRange;
        private bool         _disableStream;
        private bool         _disableAutoFetch;
        private bool         _stopAtErrors;
        private bool         _enableXfa;
        private PdfVerbosity _verbosity = PdfVerbosity.Warnings;

        private PdfSource(string url, es5.Uint8Array data)
        {
            _url  = url;
            _data = data;
        }

        /// <summary>A document fetched over HTTP.</summary>
        public static PdfSource FromUrl(string url) => new PdfSource(url, null);

        /// <summary>
        /// A document already in memory, as a JavaScript typed array.
        ///
        /// pdf.js <b>transfers</b> the array to the worker, taking ownership of it - the caller's
        /// view is detached afterwards. Opening the same array twice therefore fails the second time;
        /// pass a fresh copy, or use a URL.
        /// </summary>
        public static PdfSource FromBytes(es5.Uint8Array data) => new PdfSource(null, data);

        /// <summary>
        /// A document already in memory, as a C# array.
        ///
        /// The bytes are copied into a native <c>Uint8Array</c> rather than handed over directly: a
        /// C# array carries a <c>$type</c> property whose value is a function, and pdf.js posts the
        /// data to its worker - which refuses the whole message with a <c>DataCloneError</c> naming
        /// nothing useful.
        /// </summary>
        public static PdfSource FromBytes(byte[] data)
        {
            if (data is null) return new PdfSource(null, null);

            var copy = new es5.Uint8Array((uint)data.Length);

            for (var i = 0; i < data.Length; i++)
            {
                copy[(uint)i] = data[i];
            }

            return new PdfSource(null, copy);
        }

        /// <summary>A document held as a base64 string - what an API that embeds a PDF in JSON hands you.</summary>
        public static PdfSource FromBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return new PdfSource(null, null);

            // atob gives a binary string, one character per byte; charCodeAt is what turns that back
            // into the bytes. Going through a native Uint8Array keeps it off the C#-array path above.
            var binary = window.atob(base64);
            var bytes  = new es5.Uint8Array((uint)binary.Length);

            for (var i = 0; i < binary.Length; i++)
            {
                bytes[(uint)i] = (byte)binary[i];
            }

            return new PdfSource(null, bytes);
        }

        /// <summary>The password for an encrypted document, when it is known up front.</summary>
        public PdfSource WithPassword(string password)
        {
            _password = password;
            return this;
        }

        /// <summary>
        /// Send cookies and authorization headers with the fetch. Only needed cross-origin - the
        /// browser sends them on a same-origin request regardless.
        /// </summary>
        public PdfSource WithCredentials(bool withCredentials = true)
        {
            _withCredentials = withCredentials;
            return this;
        }

        /// <summary>Adds a request header. Call it more than once for more than one header.</summary>
        public PdfSource WithHttpHeader(string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(name)) _headers[name] = value;
            return this;
        }

        /// <summary>
        /// How many bytes each range request asks for. pdf.js defaults to 64 KB, which is a lot of
        /// requests for a large document - 512 KB or 1 MB is a better trade over a slow link.
        /// </summary>
        public PdfSource WithRangeChunkSize(int bytes)
        {
            _rangeChunkSize = bytes;
            return this;
        }

        /// <summary>
        /// The base for resolving relative URLs the document itself contains, in link annotations
        /// and outline entries that carry a relative target.
        /// </summary>
        public PdfSource WithDocumentBaseUrl(string baseUrl)
        {
            _docBaseUrl = baseUrl;
            return this;
        }

        /// <summary>
        /// Fetch the whole document in one request. Set this when the server does not honour
        /// <c>Range</c>: pdf.js works that out for itself, but only after a wasted round trip, and
        /// a server that answers a range request with the whole body confuses it entirely.
        /// </summary>
        public PdfSource WithoutRangeRequests()
        {
            _disableRange = true;
            return this;
        }

        /// <summary>Fetch the document completely before parsing any of it.</summary>
        public PdfSource WithoutStreaming()
        {
            _disableStream = true;
            return this;
        }

        /// <summary>
        /// Fetch only what the visible page needs and then stop, instead of continuing to pull the
        /// rest in the background. Saves bandwidth on a long document nobody scrolls through; costs
        /// a request every time somebody does.
        /// </summary>
        public PdfSource WithoutAutoFetch()
        {
            _disableAutoFetch = true;
            return this;
        }

        /// <summary>
        /// Fail rather than recover on a malformed document. Off by default, which is why a damaged
        /// PDF usually still renders most of itself - turn it on when a partial render would be
        /// worse than an error.
        /// </summary>
        public PdfSource StopAtErrors()
        {
            _stopAtErrors = true;
            return this;
        }

        /// <summary>Render XFA forms, for the documents that use them instead of AcroForm.</summary>
        public PdfSource WithXfa()
        {
            _enableXfa = true;
            return this;
        }

        /// <summary>How much pdf.js writes to the console while loading this document.</summary>
        public PdfSource Verbosity(PdfVerbosity verbosity)
        {
            _verbosity = verbosity;
            return this;
        }

        /// <summary>A URL is a source. Lets <c>Url(...)</c> and <c>Source(...)</c> be the same call.</summary>
        public static implicit operator PdfSource(string url) => FromUrl(url);

        /// <summary>What this describes, for a log line or an error message.</summary>
        public override string ToString() => _url ?? (_data is object ? "(" + _data.length + " bytes)" : "(empty)");

        /// <summary>
        /// The pdf.js parameter object for this source, with the package's asset URLs filled in.
        ///
        /// A fresh object every call, deliberately: the byte array in it is transferred to the worker
        /// and the caller's view of it detached, so a cached parameter object could only be opened
        /// once.
        /// </summary>
        internal DocumentInitParameters ToInitParameters()
        {
            var parameters = new DocumentInitParameters
            {
                // Assigned unconditionally, because these are what make CJK text, unembedded fonts,
                // JPX/JBIG2 images and CMYK colour work at all - and pdf.js only warns when they are
                // missing, so leaving one out produces a document that renders slightly wrong.
                cMapUrl             = PdfJs.CMapUrl,
                cMapPacked          = true,
                standardFontDataUrl = PdfJs.StandardFontDataUrl,
                wasmUrl             = PdfJs.WasmUrl,
                iccUrl              = PdfJs.IccUrl,
                verbosity           = _verbosity,
            };

            // Everything below is only assigned when it was asked for: an [ObjectLiteral] emits just
            // the fields that were set, so an untouched option keeps pdf.js's own default rather than
            // being overridden with a C# zero.
            if (!string.IsNullOrWhiteSpace(_url))        parameters.url        = _url;
            if (_data is object)                         parameters.data       = _data;
            if (!string.IsNullOrEmpty(_password))        parameters.password   = _password;
            if (_withCredentials)                        parameters.withCredentials = true;
            if (_rangeChunkSize > 0)                     parameters.rangeChunkSize  = _rangeChunkSize;
            if (!string.IsNullOrWhiteSpace(_docBaseUrl)) parameters.docBaseUrl = _docBaseUrl;
            if (_disableRange)                           parameters.disableRange     = true;
            if (_disableStream)                          parameters.disableStream    = true;
            if (_disableAutoFetch)                       parameters.disableAutoFetch = true;
            if (_stopAtErrors)                           parameters.stopAtErrors     = true;
            if (_enableXfa)                              parameters.enableXfa        = true;

            if (_headers.Count > 0)
            {
                var headers = new HttpHeaders();

                foreach (var header in _headers)
                {
                    Script.Set(headers, header.Key, header.Value);
                }

                parameters.httpHeaders = headers;
            }

            return parameters;
        }
    }

    /// <summary>
    /// A bag of request headers, keyed by name.
    ///
    /// An empty <c>[ObjectLiteral]</c> is a bare <c>{ }</c> at runtime, which is what pdf.js wants -
    /// header names are not valid C# identifiers, so they are written with <c>Script.Set</c> rather
    /// than declared as fields.
    /// </summary>
    [ObjectLiteral]
    public class HttpHeaders
    {
    }
}
