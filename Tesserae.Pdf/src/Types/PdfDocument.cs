using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Transpose;
using Transpose.Core;

namespace Tesserae.Pdf
{
    /// <summary>
    /// A loaded PDF, and everything that can be asked of it without putting it on screen: its pages,
    /// its outline, its metadata, its text, its bytes.
    ///
    /// <b>Whoever opens a document owns it.</b> A document holds a worker-side copy of the whole
    /// file, so it has to be released with <see cref="DestroyAsync"/> - or, when a component opened
    /// it, by that component's teardown. Opening a document per thumbnail and never releasing them is
    /// the leak this type is shaped to avoid: <see cref="GetPageAsync"/> is what a thumbnail rail
    /// should reach for, off one shared document.
    ///
    /// Note there is no <c>Destroy</c> on the document itself. pdf.js 6 removed
    /// <c>PDFDocumentProxy.destroy()</c>, and the loading task is what owns the lifetime now - which
    /// is why this type keeps hold of it.
    /// </summary>
    public sealed class PdfDocument
    {
        private readonly IPdfDocumentLoadingTask _loadingTask;
        private readonly IPdfDocumentProxy       _document;

        private bool _destroyed;

        internal PdfDocument(IPdfDocumentLoadingTask loadingTask, IPdfDocumentProxy document)
        {
            _loadingTask = loadingTask;
            _document    = document;
        }

        /// <summary>How many pages the document has.</summary>
        public int PageCount => _document.numPages;

        /// <summary>
        /// The document's <c>/ID</c> pair. The first entry identifies the file, the second is null
        /// unless it has been modified since it was created - together they make a reasonable cache
        /// key.
        /// </summary>
        public string[] Fingerprints => _document.fingerprints;

        /// <summary>The underlying pdf.js document, for anything this wrapper does not cover.</summary>
        public IPdfDocumentProxy Instance => _document;

        /// <summary>
        /// The pdf.js loading task that produced this document. Also what releases it - see
        /// <see cref="DestroyAsync"/>.
        /// </summary>
        public IPdfDocumentLoadingTask LoadingTask => _loadingTask;

        /// <summary>Whether this document has been released.</summary>
        public bool IsDestroyed => _destroyed;

        /// <summary>One page, 1-based. pdf.js caches these, so asking twice is cheap.</summary>
        public async Task<PdfPage> GetPageAsync(int pageNumber)
        {
            if (pageNumber < 1 || pageNumber > PageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page " + pageNumber + " is outside a document of " + PageCount + " pages.");
            }

            var page = await PromiseHelper.ToTask<IPdfPageProxy>(_document.getPage(pageNumber));

            return new PdfPage(page);
        }

        /// <summary>
        /// The document's outline as a tree, or an empty list when it has none - which most PDFs do
        /// not, so an empty result is the common case rather than a failure.
        /// </summary>
        public async Task<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync()
        {
            // Awaited as object and cast, not as IOutlineNode[]: an array type argument is
            // materialised at runtime by System.Array.type(element), which needs metadata an
            // [External] interface has none of. See PromiseHelper.ToTask.
            var resolved = await PromiseHelper.ToTask<object>(_document.getOutline());
            var nodes    = (IOutlineNode[])resolved;
            var items    = new List<PdfOutlineItem>();

            if (nodes is null) return items;

            foreach (var node in nodes)
            {
                items.Add(new PdfOutlineItem(node));
            }

            return items;
        }

        /// <summary>
        /// The document's title, author, dates and format flags. Every field is optional in PDF, so
        /// most of them are usually null.
        /// </summary>
        public async Task<PdfMetadata> GetMetadataAsync()
        {
            var result = await PromiseHelper.ToTask<IMetadataResult>(_document.getMetadata());

            return new PdfMetadata(result);
        }

        /// <summary>
        /// The labels the document wants its pages called - roman numerals for front matter, a
        /// restart at 1 for the body - or null when it just uses the page numbers. A viewer shows
        /// these instead of the page number when they exist.
        /// </summary>
        public Task<string[]> GetPageLabelsAsync() => PromiseHelper.ToTask<string[]>(_document.getPageLabels());

        /// <summary>
        /// What the document permits, or null when it places no restrictions at all.
        ///
        /// The null is the point: pdf.js reports nothing for an unrestricted document, and an empty
        /// list for one that forbids everything, so the two cannot be collapsed. None of it is
        /// enforcement - a PDF's permission flags are a request to the viewer.
        /// </summary>
        public async Task<PdfPermission[]> GetPermissionsAsync()
        {
            var flags = await PromiseHelper.ToTask<int[]>(_document.getPermissions());

            if (flags is null) return null;

            var permissions = new PdfPermission[flags.Length];

            for (var i = 0; i < flags.Length; i++)
            {
                permissions[i] = (PdfPermission)flags[i];
            }

            return permissions;
        }

        /// <summary>Whether the document permits something. True for a document with no restrictions.</summary>
        public async Task<bool> IsAllowedAsync(PdfPermission permission)
        {
            var permissions = await GetPermissionsAsync();

            if (permissions is null) return true;

            foreach (var granted in permissions)
            {
                if (granted == permission) return true;
            }

            return false;
        }

        /// <summary>
        /// The files embedded in the document, or an empty list when there are none.
        ///
        /// pdf.js hands these back as a JavaScript <c>Map</c>, which is why this walks it rather than
        /// reading keys off an object.
        /// </summary>
        public async Task<IReadOnlyList<PdfAttachment>> GetAttachmentsAsync()
        {
            var map         = await PromiseHelper.ToTask<es5.Map<string, IPdfAttachment>>(_document.getAttachments());
            var attachments = new List<PdfAttachment>();

            // es5.Map is safe as a type argument where an external-interface array is not: it is
            // emitted as the global `Map`, which exists, and its own type arguments are not
            // materialised.

            if (map is null) return attachments;

            map.forEach((value, key, _) => attachments.Add(new PdfAttachment(key, value)));

            return attachments;
        }

        /// <summary>
        /// The named destinations the document declares, keyed by name. Each value is pdf.js's own
        /// destination form, to be handed to <c>PdfViewer.GoToDestination</c> unchanged.
        /// </summary>
        public async Task<IReadOnlyDictionary<string, object>> GetNamedDestinationsAsync()
        {
            var map          = await PromiseHelper.ToTask<es5.Map<string, object>>(_document.getDestinations());
            var destinations = new Dictionary<string, object>();

            if (map is null) return destinations;

            map.forEach((value, key, _) => destinations[key] = value);

            return destinations;
        }

        /// <summary>
        /// The 1-based page a destination points at, or 0 when it points at nothing this document can
        /// resolve.
        ///
        /// Takes the value <see cref="PdfOutlineItem.Destination"/> carries, in either of the two
        /// forms a PDF destination comes in: the name of a named destination, or an explicit array
        /// whose first element is the target page. So this is what turns "Capacity planning" in an
        /// outline into "page 11" - which is what an outline panel shows beside each entry, and what
        /// tells it which entry the reader is currently inside.
        ///
        /// <b>Three round trips in the worst case</b> - the named-destination lookup, then the page
        /// reference - so resolve an outline once and keep the answers rather than asking per repaint.
        /// pdf.js caches on its side, but the calls still cross to the worker.
        ///
        /// Returns 0 rather than throwing for an entry that has no target, names a destination the
        /// document does not declare, or points into a different file: an outline with a few dead
        /// entries is ordinary, and a panel should draw the rest of it.
        /// </summary>
        public async Task<int> GetDestinationPageAsync(object destination)
        {
            if (destination is null) return 0;

            var explicitDestination = destination;

            // A named destination has to be looked up first; an explicit one is already the array.
            if (Script.TypeOf(destination) == "string")
            {
                explicitDestination = await PromiseHelper.ToTask<object>(_document.getDestination((string)destination));
            }

            if (explicitDestination is null) return 0;

            var parts = (object[])explicitDestination;

            if (parts is null || parts.Length == 0) return 0;

            var target = parts[0];

            if (target is null) return 0;

            // The first element is either a page reference - an object carrying the object number and
            // generation - or a 0-based page index that pdf.js has already resolved. Which one it is
            // is a question about its JavaScript type and nothing else, which is why this asks
            // typeof rather than testing against a C# type: `target is double` would go through the
            // runtime's type metadata, and what arrives here is a plain JavaScript value.
            if (Script.TypeOf(target) == "number")
            {
                var index = (int)(double)target;

                return index >= 0 && index < PageCount ? index + 1 : 0;
            }

            int resolved;

            try
            {
                resolved = await PromiseHelper.ToTask<int>(_document.getPageIndex(target));
            }
            catch (Exception)
            {
                // A reference to a page in another document, or a broken one. Not worth reporting:
                // the caller asked where an entry points, and the answer is "nowhere here".
                return 0;
            }

            return resolved >= 0 && resolved < PageCount ? resolved + 1 : 0;
        }

        /// <summary>The document's bytes as pdf.js holds them - the file as fetched, without form edits.</summary>
        public Task<es5.Uint8Array> GetDataAsync() => PromiseHelper.ToTask<es5.Uint8Array>(_document.getData());

        /// <summary>
        /// The document's bytes including whatever the user typed into its form fields. This is what
        /// "save a filled form" means - <see cref="GetDataAsync"/> gives back the original.
        /// </summary>
        public Task<es5.Uint8Array> SaveAsync() => PromiseHelper.ToTask<es5.Uint8Array>(_document.saveDocument());

        /// <summary>Whether the document carries embedded JavaScript, i.e. whether scripting has anything to run.</summary>
        public Task<bool> HasEmbeddedJavaScriptAsync() => PromiseHelper.ToTask<bool>(_document.hasJSActions());

        /// <summary>
        /// Every page's text, concatenated, with a form feed between pages.
        ///
        /// Pages are read one after another rather than in parallel: the worker serialises them
        /// anyway, and asking for fifty at once only makes the memory spike worse.
        /// </summary>
        public async Task<string> GetAllTextAsync()
        {
            var text = new StringBuilder();

            for (var pageNumber = 1; pageNumber <= PageCount; pageNumber++)
            {
                var page = await GetPageAsync(pageNumber);

                if (pageNumber > 1) text.Append("\f");

                text.Append(await page.GetTextAsync());
            }

            return text.ToString();
        }

        /// <summary>
        /// Releases the worker-side caches for pages nothing is displaying. Cheap, and worth doing
        /// after scrolling through a long document; pdf.js re-fetches whatever it needs again.
        /// </summary>
        public Task CleanupAsync() => PromiseHelper.ToTask(_document.cleanup(false));

        /// <summary>
        /// Releases the document and the worker's copy of it. Idempotent.
        ///
        /// Done through the loading task, which is where the whole teardown lives in pdf.js 6 -
        /// <c>PDFDocumentProxy.destroy()</c> no longer exists. A viewer showing this document must be
        /// told to let go of it first (<c>SetDocument(null)</c>), or it holds pages of a document that
        /// has gone.
        /// </summary>
        public Task DestroyAsync()
        {
            if (_destroyed) return Task.CompletedTask;

            _destroyed = true;

            return PromiseHelper.ToTask(_loadingTask.destroy());
        }
    }

    /// <summary>An embedded file, as pdf.js reports it.</summary>
    [External]
    [Convention(Notation.None)]
    public interface IPdfAttachment
    {
        /// <summary>The name to show, without a path.</summary>
        string filename { get; }

        /// <summary>The name as the document stores it, which may carry a path.</summary>
        string rawFilename { get; }

        /// <summary>The document's own description of the file, when it gives one.</summary>
        string description { get; }

        /// <summary>The bytes, when pdf.js already had them. Null when they have to be fetched.</summary>
        es5.Uint8Array content { get; }
    }

    /// <summary>One file embedded in a document.</summary>
    public sealed class PdfAttachment
    {
        internal PdfAttachment(string key, IPdfAttachment attachment)
        {
            Key         = key;
            FileName    = attachment.filename;
            RawFileName = attachment.rawFilename;
            Description = attachment.description;
            Content     = attachment.content;
        }

        /// <summary>The key the document files it under, and what <c>getAttachmentContent</c> takes.</summary>
        public string Key { get; }

        /// <summary>The name to show.</summary>
        public string FileName { get; }

        /// <summary>The name as stored, path and all.</summary>
        public string RawFileName { get; }

        /// <summary>The document's description of the file, or null.</summary>
        public string Description { get; }

        /// <summary>
        /// The bytes, when pdf.js had them to hand. Null for an attachment that has to be fetched
        /// separately, which is what <c>getAttachmentContent</c> on the underlying document is for.
        /// </summary>
        public es5.Uint8Array Content { get; }

        /// <summary>The size in bytes, or -1 when the content has not been fetched.</summary>
        public int Length => Content is object ? (int)Content.length : -1;
    }

    /// <summary>
    /// A document's information dictionary and XMP stream, flattened into something readable.
    ///
    /// Every field is optional in PDF and most documents carry only a few, so null is the normal
    /// answer rather than a sign anything went wrong.
    /// </summary>
    public sealed class PdfMetadata
    {
        private readonly IMetadata _xmp;

        internal PdfMetadata(IMetadataResult result)
        {
            var info = result?.info;

            if (info is object)
            {
                Title             = info.Title;
                Author            = info.Author;
                Subject           = info.Subject;
                Keywords          = info.Keywords;
                Creator           = info.Creator;
                Producer          = info.Producer;
                CreationDate      = info.CreationDate;
                ModifiedDate      = info.ModDate;
                PdfVersion        = info.PDFFormatVersion;
                Language          = info.Language;
                IsLinearized      = info.IsLinearized;
                HasAcroForm       = info.IsAcroFormPresent;
                HasXfa            = info.IsXFAPresent;
                HasSignatures     = info.IsSignaturesPresent;
                IsPortfolio       = info.IsCollectionPresent;
            }

            _xmp = result?.metadata;
        }

        public string Title        { get; }
        public string Author       { get; }
        public string Subject      { get; }
        public string Keywords     { get; }

        /// <summary>The application the document was authored in.</summary>
        public string Creator { get; }

        /// <summary>The library that wrote the PDF itself.</summary>
        public string Producer { get; }

        /// <summary>A PDF date string, e.g. <c>"D:20260501120000Z"</c>. Not parsed - PDF's format is its own.</summary>
        public string CreationDate { get; }

        /// <summary>A PDF date string.</summary>
        public string ModifiedDate { get; }

        /// <summary>The PDF specification version the file claims, e.g. <c>"1.7"</c>.</summary>
        public string PdfVersion { get; }

        /// <summary>The document's declared language, when it has one.</summary>
        public string Language { get; }

        /// <summary>
        /// Whether the file is laid out for progressive download. A linearized document can show its
        /// first page before the rest has arrived; one that is not has to be fetched whole.
        /// </summary>
        public bool IsLinearized { get; }

        /// <summary>Whether the document has an AcroForm, i.e. fillable fields.</summary>
        public bool HasAcroForm { get; }

        /// <summary>Whether it uses XFA forms instead, which need <c>WithXfa()</c> to render.</summary>
        public bool HasXfa { get; }

        /// <summary>Whether it carries digital signatures.</summary>
        public bool HasSignatures { get; }

        /// <summary>Whether it is a portfolio - a container of other files rather than a document.</summary>
        public bool IsPortfolio { get; }

        /// <summary>
        /// One XMP property by name, e.g. <c>"dc:title"</c>, or null when the document has no XMP
        /// stream or does not carry that property. XMP is the modern half of PDF metadata and often
        /// disagrees with the information dictionary above; where they do, XMP is meant to win.
        /// </summary>
        public string GetXmp(string name)
        {
            if (_xmp is null || string.IsNullOrWhiteSpace(name)) return null;

            var value = _xmp.get(name);

            return value is object ? value.ToString() : null;
        }

        /// <summary>The raw XMP stream, or null when the document has none.</summary>
        public string RawXmp => _xmp?.getRaw();
    }
}
