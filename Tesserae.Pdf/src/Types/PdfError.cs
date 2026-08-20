using System;
using Transpose;

namespace Tesserae.Pdf
{
    /// <summary>
    /// The runtime's wrapper around a rejected JavaScript promise.
    ///
    /// A faulted <see cref="System.Threading.Tasks.Task"/> that came from a promise does not carry
    /// the value the promise rejected with: the runtime wraps it in a <c>PromiseException</c> whose
    /// <c>arguments</c> array holds the real rejection. Without unwrapping that, every pdf.js failure
    /// reads as the same generic exception and a 404 cannot be told from a corrupt file.
    ///
    /// Declared by shape rather than named as a type, because the cast is erased - so reading
    /// <c>arguments</c> off something that is not a wrapper simply gives <c>undefined</c>.
    /// </summary>
    [External]
    [Convention(Notation.None)]
    internal interface IPromiseRejection
    {
        object[] arguments { get; }
    }

    /// <summary>What went wrong, in the terms a host actually branches on.</summary>
    public enum PdfErrorKind
    {
        /// <summary>Not one of the below - or a failure that came from outside pdf.js.</summary>
        Unknown = 0,

        /// <summary>
        /// The document is encrypted. Handled by the viewer's password callback rather than surfaced
        /// as an error, unless nothing answers it.
        /// </summary>
        Password,

        /// <summary>The bytes are not a PDF, or are damaged past pdf.js's ability to recover.</summary>
        InvalidPdf,

        /// <summary>
        /// The fetch failed. <see cref="PdfError.Status"/> carries the HTTP status and
        /// <see cref="PdfError.Missing"/> says whether it means the document is absent.
        /// </summary>
        Response,

        /// <summary>The load was abandoned - usually because the viewer was given another document.</summary>
        Aborted,

        /// <summary>
        /// A page render was cancelled, which is an ordinary outcome rather than a failure: it is what
        /// happens when the user scrolls away or zooms while a page is still painting.
        /// </summary>
        RenderingCancelled,
    }

    /// <summary>
    /// A pdf.js failure, as a C# exception.
    ///
    /// pdf.js's own exception types cannot be told apart with <c>is</c> from outside its bundle -
    /// they derive from a pseudo-class rather than from <c>Error</c>, so a type test reads metadata
    /// that is not there. Their <c>name</c> string is the discriminator, and
    /// <see cref="FromJs"/> is the one place this package reads it.
    /// </summary>
    public class PdfError : Exception
    {
        internal PdfError(PdfErrorKind kind, string name, string message, int status, bool missing)
            : base(string.IsNullOrEmpty(message) ? (name ?? "pdf.js error") : message)
        {
            Kind    = kind;
            Name    = name;
            Status  = status;
            Missing = missing;
        }

        /// <summary>What kind of failure this is.</summary>
        public PdfErrorKind Kind { get; }

        /// <summary>pdf.js's own class name, e.g. <c>"ResponseException"</c>. Null when the failure did not come from pdf.js.</summary>
        public string Name { get; }

        /// <summary>The HTTP status, on a <see cref="PdfErrorKind.Response"/>. Zero otherwise.</summary>
        public int Status { get; }

        /// <summary>
        /// True when a <see cref="PdfErrorKind.Response"/> means the document is not there, as
        /// opposed to being refused or having failed on the way. pdf.js decides this from the status.
        /// </summary>
        public bool Missing { get; }

        /// <summary>
        /// True for the one failure that is not really a failure: a page render that was cancelled
        /// because the view moved on. Worth checking before reporting anything to a user.
        /// </summary>
        public bool IsCancellation => Kind == PdfErrorKind.RenderingCancelled || Kind == PdfErrorKind.Aborted;

        /// <summary>
        /// Turns whatever a pdf.js promise rejected with into a <see cref="PdfError"/>.
        ///
        /// Everything is admitted, including a rejection that is not a pdf.js exception at all: a
        /// faulted task on this path has to become something a host can catch, and
        /// <see cref="PdfErrorKind.Unknown"/> is a better answer than a rethrow of an object with no
        /// stack.
        /// </summary>
        internal static PdfError FromJs(object error)
        {
            if (error is PdfError already) return already;

            // A rejected JavaScript promise does not arrive as the value it rejected with: the
            // runtime wraps it in a Transpose.PromiseException whose Arguments carry the real
            // rejection, and whose own name is "PromiseException". Reading the discriminator off the
            // wrapper classifies every pdf.js failure as Unknown - a 404 and a corrupt file become
            // indistinguishable - so the wrapper is peeled off first.
            // Read by shape rather than by type: the wrapper is reached through a cast that emits
            // nothing, so `arguments` is simply undefined on anything that is not one.
            var rejection = ((IPromiseRejection)error)?.arguments;

            if (rejection is object && rejection.Length > 0) return FromJs(rejection[0]);

            // A direct cast, never `as`: a type test against an [External] interface has no runtime
            // metadata to test against and throws instead of answering false. The cast is erased, so
            // reading a member that is not there gives undefined rather than failing.
            var jsError = (IPdfJsError)error;

            var name    = jsError is object ? jsError.name    : null;
            var message = jsError is object ? jsError.message : null;
            var status  = jsError is object ? jsError.status  : 0;
            var missing = jsError is object && jsError.missing;

            // pdf.js renamed these in version 5: MissingPDFException and UnexpectedResponseException
            // both became ResponseException, which carries the status instead of encoding it in the
            // type. The old names are still matched so a host reading an error from an older pdf.js
            // (or a mixed page) gets the same kind.
            switch (name)
            {
                case "PasswordException":            return new PdfError(PdfErrorKind.Password,   name, message, status, missing);
                case "InvalidPDFException":          return new PdfError(PdfErrorKind.InvalidPdf, name, message, status, missing);
                case "ResponseException":            return new PdfError(PdfErrorKind.Response,   name, message, status, missing);
                case "MissingPDFException":          return new PdfError(PdfErrorKind.Response,   name, message, status, true);
                case "UnexpectedResponseException":  return new PdfError(PdfErrorKind.Response,   name, message, status, missing);
                case "AbortException":               return new PdfError(PdfErrorKind.Aborted,    name, message, status, missing);
                case "RenderingCancelledException":  return new PdfError(PdfErrorKind.RenderingCancelled, name, message, status, missing);
            }

            // Not a pdf.js exception. A real C# exception keeps its message; anything else is
            // stringified, because an opaque rejection with no message at all is the worst thing to
            // hand a host.
            if (error is Exception exception)
            {
                return new PdfError(PdfErrorKind.Unknown, exception.GetType().Name, exception.Message, 0, false);
            }

            return new PdfError(PdfErrorKind.Unknown, name, message ?? (error is object ? error.ToString() : null), status, missing);
        }
    }
}
