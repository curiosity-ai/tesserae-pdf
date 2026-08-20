using System;
using System.Threading.Tasks;

namespace Tesserae.Pdf
{
    public static partial class PdfJs
    {
        /// <summary>
        /// Opens a document without putting it on screen, for the things that do not need a viewer:
        /// extracting text, reading metadata, counting pages, rendering a page into a canvas of your
        /// own.
        ///
        /// pdf.js is loaded on the first call, so this works before anything has mounted.
        ///
        /// <b>The caller owns the result</b> and has to release it with
        /// <see cref="PdfDocument.DestroyAsync"/> - a document holds a worker-side copy of the whole
        /// file. When several pages of the same document are wanted, open it once and take pages off
        /// it rather than opening it per page.
        /// </summary>
        /// <param name="source">Where the document comes from. A plain URL string works.</param>
        /// <param name="onProgress">
        /// Called as bytes arrive, with how many of how many. Total is 0 when the server sends no
        /// content length, which is also when pdf.js's own <c>percent</c> comes back as NaN.
        /// </param>
        /// <param name="password">
        /// Asked for the password when the document turns out to be encrypted, and asked again if the
        /// one it gives is wrong. Returning null gives up, which fails the open with
        /// <see cref="PdfErrorKind.Password"/>.
        /// </param>
        public static async Task<PdfDocument> OpenAsync(PdfSource source, Action<double, double> onProgress = null, Func<PasswordReason, Task<string>> password = null)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            await LoadAsync();

            var loadingTask = PdfJsLib.getDocument(source.ToInitParameters());

            if (onProgress is object)
            {
                // pdf.js's own percent is NaN when the response carries no length, so the two numbers
                // are handed over raw and the caller decides what an unknown total means.
                loadingTask.onProgress = progress => onProgress(progress.loaded, progress.total);
            }

            if (password is object)
            {
                loadingTask.onPassword = (updatePassword, reason) => AnswerPasswordAsync(updatePassword, reason, password, loadingTask).FireAndForget();
            }

            try
            {
                var document = await PromiseHelper.ToTask<IPdfDocumentProxy>(loadingTask.promise);

                return new PdfDocument(loadingTask, document);
            }
            catch (Exception exception)
            {
                // The task holds a worker even when the load failed, so it has to be released before
                // the error goes up - otherwise a page that retries a bad URL leaks one per attempt.
                if (!loadingTask.destroyed) loadingTask.destroy();

                throw PdfError.FromJs(exception);
            }
        }

        /// <summary>
        /// Bridges pdf.js's synchronous password callback to an async one.
        ///
        /// pdf.js hands over a function to call with the password and does not wait for it: the load
        /// simply stays pending until it is called. That is what makes a dialog possible here - and
        /// what makes "never answer" a hang rather than an error, which is why not producing a
        /// password destroys the task instead of returning.
        /// </summary>
        private static async Task AnswerPasswordAsync(Action<string> updatePassword, int reason, Func<PasswordReason, Task<string>> password, IPdfDocumentLoadingTask loadingTask)
        {
            string answer = null;

            try
            {
                answer = await password((PasswordReason)reason);
            }
            catch (Exception exception)
            {
                Transpose.Core.dom.console.error("Tesserae.Pdf: the password callback threw", exception);
            }

            if (string.IsNullOrEmpty(answer))
            {
                if (!loadingTask.destroyed) loadingTask.destroy();

                return;
            }

            updatePassword(answer);
        }
    }
}
