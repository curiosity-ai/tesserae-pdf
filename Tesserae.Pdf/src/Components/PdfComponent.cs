using System;
using System.Threading.Tasks;
using Transpose;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Pdf
{
    /// <summary>
    /// Shared plumbing for the pdf.js-backed components: a sized container element, the
    /// mount/create/dispose lifecycle, and keeping the underlying view in step with the container's
    /// size.
    ///
    /// pdf.js can only size itself once it is in the document - a viewer measures its container to
    /// decide what "fit the width" means, and a page render needs a real width to pick a scale - so
    /// the underlying object is created lazily on mount rather than in the constructor. Everything
    /// configured before that point is captured in fields and applied when it is created; everything
    /// configured afterwards is forwarded to the live instance. Each component's property setters
    /// follow that same "field if not created yet, otherwise forward" shape.
    ///
    /// A component is <b>remountable</b>. Leaving the DOM tears the view down - the alternative leaks
    /// a document and its worker per detach - but the component re-arms itself, so being added back
    /// builds a new one and replays everything that was configured. That is what a component moved
    /// between containers, or inside a parent that detaches rather than hides, needs.
    /// <see cref="Dispose"/> is the one-way door: it opts out of that and releases the component for
    /// good.
    /// </summary>
    public abstract class PdfComponent : IComponent, ISpecialCaseStyling
    {
        private readonly HTMLElement    _container;
        private          ResizeObserver _resizeObserver;
        private          bool           _mountRequested;
        private          bool           _created;
        private          bool           _disposed;

        /// <summary>
        /// Teardown this component owns - event-bus subscriptions, observers, in-flight renders -
        /// released together when the view is torn down.
        /// </summary>
        protected DisposableBag Disposables { get; } = new DisposableBag();

        protected PdfComponent()
        {
            _container = DIV();

            _container.style.width    = "100%";
            _container.style.height   = "100%";
            _container.style.overflow = "hidden";

            // Relative, not static: pdf.js's viewer needs an absolutely-positioned scroll host, and
            // this is what that host is positioned against. It doubles as the outer wrapper the
            // Tesserae sizing helpers write to.
            _container.style.position = "relative";
        }

        /// <summary>The container element - styled directly by the Tesserae sizing helpers.</summary>
        public HTMLElement StylingContainer => _container;

        /// <summary>
        /// Sizing helpers stay on the container and are not tagged for a wrapper-building container
        /// (Masonry, SectionStack, KeyedObservableStack) to hoist: pdf.js measures the element it was
        /// created in, and hoisting the height onto a wrapper clears it here, leaving the viewer with
        /// nothing to size against - which shows up as a viewer of zero height rather than an error.
        /// </summary>
        public bool PropagateStylesToWrapper => false;

        /// <summary>Whether the underlying pdf.js view has been built.</summary>
        protected bool IsCreated => _created;

        /// <summary>Whether <see cref="Dispose"/> has been called.</summary>
        public bool IsDisposed => _disposed;

        public HTMLElement Render()
        {
            if (!_mountRequested)
            {
                _mountRequested = true;
                ArmMountObserver();
            }

            return _container;
        }

        private void ArmMountObserver()
        {
            DomObserver.WhenMounted(_container, () => MountAsync().FireAndForget());
        }

        private async Task MountAsync()
        {
            await PdfJs.LoadAsync();

            // The component can be discarded again, or torn down and remounted, while pdf.js loads.
            if (_disposed || !_container.IsMounted()) return;

            // A second mount signal for a view that already exists would create a duplicate.
            if (_created) return;

            await WaitForAncestorAnimationsAsync();

            // Waiting yielded to the browser, so re-check all three of the above.
            if (_disposed || !_container.IsMounted() || _created) return;

            _created = true;

            CreateCore(_container);

            _resizeObserver = new ResizeObserver((_, __) => OnResized());
            _resizeObserver.observe(_container);

            DomObserver.WhenRemoved(_container, HandleRemoved);

            AfterCreate();
        }

        /// <summary>
        /// Holds the view back until no ancestor is mid-animation.
        ///
        /// pdf.js sizes a page's scroll layer to millions of pixels, and Chromium rasters an
        /// animating layer's whole subtree rather than the part in view. A layer that big inside an
        /// ancestor running a transform animation that starts near zero scale - Tesserae's
        /// <c>tss-modal-animation</c> historically started at <c>scale(0)</c>, a singular matrix -
        /// makes the raster work unbounded, and the renderer stops producing frames <b>for the whole
        /// page</b>: rAF never fires again, <c>document.timeline</c> stops, and every screenshot,
        /// keystroke and click hangs waiting for a frame that never comes. The main thread stays
        /// responsive throughout, which is what makes it look like a crash rather than a stall.
        ///
        /// Creating the view one frame later is enough - the stall only happens while the ancestor's
        /// scale is under about 0.01, and an animation has climbed out of that range by its second
        /// frame. Waiting for the animation to finish outright also gets pdf.js a container whose
        /// <c>getBoundingClientRect</c> is not scaled, which is what its sizing reads: during a
        /// modal's open animation, an element inside it measures as zero height even when its style
        /// says otherwise.
        ///
        /// Bounded on both sides: only an animation that ends within
        /// <see cref="MAX_ANIMATION_WAIT_MS"/> is waited for - an infinite one (a spinner, a shimmer)
        /// reports <c>Infinity</c> and is ignored - and the loop itself gives up at the same limit, so
        /// an ancestor that keeps restarting its animation delays the view rather than withholding it.
        /// </summary>
        private async Task WaitForAncestorAnimationsAsync()
        {
            for (var waited = 0; waited < MAX_ANIMATION_WAIT_MS && HasAnimatingAncestor(); waited += ANIMATION_POLL_MS)
            {
                await Task.Delay(ANIMATION_POLL_MS);
            }
        }

        private bool HasAnimatingAncestor()
        {
            var animations = document.getAnimations();

            if (animations is null) return false;

            foreach (var animation in animations)
            {
                if (animation.playState != "running") continue;

                // AnimationEffectReadOnly does not carry a target; a CSS animation, a transition and
                // a WAAPI animation all have a KeyframeEffect, which does. A direct cast to it emits
                // nothing - `as`/`is` would emit a runtime type test against metadata a dom type has
                // none of, and throw rather than answer false.
                var effect = (KeyframeEffect)animation.effect;

                if (effect is null) continue;

                var target = effect.target;

                // contains() answers true for the element itself, which is what we want: the
                // container is as much of a problem animating itself as an ancestor animating it.
                if (target is null || !target.contains(_container)) continue;

                // An animation that never ends would hold the view back for good; one that ends
                // beyond the limit would be cut short by the loop anyway.
                if (!(effect.getComputedTiming().endTime < MAX_ANIMATION_WAIT_MS)) continue;

                return true;
            }

            return false;
        }

        private const int ANIMATION_POLL_MS     = 16;
        private const int MAX_ANIMATION_WAIT_MS = 1000;

        // Leaving the DOM tears the view down but keeps the component usable: the mount observer is
        // re-armed, so being added back rebuilds it and replays the configuration. Without the
        // teardown a detached viewer leaks a document and a worker; without the re-arm the component
        // silently renders an empty container ever after.
        private void HandleRemoved()
        {
            if (_disposed) return;

            Teardown();
            ArmMountObserver();
        }

        private void Teardown()
        {
            if (!_created) return;

            BeforeDispose();

            Disposables.DisposeAll();

            if (_resizeObserver != null)
            {
                _resizeObserver.disconnect();
                _resizeObserver = null;
            }

            DisposeCore();

            _created = false;
        }

        /// <summary>Builds the underlying pdf.js view inside <paramref name="container"/>.</summary>
        protected abstract void CreateCore(HTMLElement container);

        /// <summary>Releases the underlying pdf.js view. Called on teardown as well as on dispose.</summary>
        protected abstract void DisposeCore();

        /// <summary>Called once the view exists, for per-component wiring and replay.</summary>
        protected virtual void AfterCreate() { }

        /// <summary>
        /// Called before the view is torn down - on leaving the DOM as well as on
        /// <see cref="Dispose"/>. Capture anything that should survive a remount here.
        /// </summary>
        protected virtual void BeforeDispose() { }

        /// <summary>Called when the container's size changes.</summary>
        protected virtual void OnResized() { }

        /// <summary>
        /// Releases the component for good: tears the view down and stops it being rebuilt if the
        /// container is mounted again. Leaving the DOM does <b>not</b> call this - it tears down and
        /// re-arms - so call it explicitly when a component is genuinely finished with.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            Teardown();
        }
    }
}
