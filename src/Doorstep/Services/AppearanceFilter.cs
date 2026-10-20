namespace Doorstep;

// Visual-only darkening. The page is color-inverted. Large photos, video, and
// snapshot backgrounds are inverted back. Small icons are not, so dark artwork
// turns light and stays visible.
internal static class AppearanceFilter
{
    internal static readonly string Script = """
        (() => {
          const photoClass = "doorstep-photo";
          const reverse = "invert(100%) hue-rotate(180deg)";
          let topFrame = false;
          try { topFrame = window === window.top; } catch (e) { topFrame = false; }

          const reverseDecl = "-webkit-filter:" + reverse + " !important;filter:" + reverse + " !important;";
          const scroll = "scrollbar-color:rgb(158,168,178) rgb(16,20,24) !important;";
          const page = topFrame
            ? "html{color-scheme:light !important;background:rgb(255,255,255) !important;" + reverseDecl + scroll + "}"
            : "html{color-scheme:light !important;" + scroll + "}";
          const css = "@media screen{" + page +
            ":-webkit-full-screen,:-webkit-full-screen *,:fullscreen,:fullscreen *{-webkit-filter:none !important;filter:none !important;}" +
            "." + photoClass + ",:fullscreen ." + photoClass + ",:-webkit-full-screen ." + photoClass + "{" + reverseDecl + "}}";

          const seenRoots = new WeakSet();
          let scheduled = false;

          function ensureStyle() {
            const root = document.documentElement;
            if (!root) return false;
            if (!document.getElementById("doorstep-appearance")) {
              const style = document.createElement("style");
              style.id = "doorstep-appearance";
              style.textContent = css;
              root.appendChild(style);
            }
            return true;
          }

          function isLarge(size) {
            return size.w >= 48 && size.h >= 48 && Math.max(size.w, size.h) >= 96;
          }

          function box(el) {
            const rect = el.getBoundingClientRect();
            return { w: rect.width, h: rect.height };
          }

          function coversViewport(el) {
            const size = box(el);
            const w = window.innerWidth || 1;
            const h = window.innerHeight || 1;
            return size.w >= w * 0.9 && size.h >= h * 0.9;
          }

          function backgroundLooksLikePhoto(style) {
            const background = style.backgroundImage || "";
            if (background.indexOf("url(") < 0) return false;
            const size = (style.backgroundSize || "").toLowerCase();
            if (size.indexOf("cover") >= 0 || size.indexOf("100%") >= 0) return true;
            const px = size.match(/([\d.]+)px/g);
            if (!px || px.length < 2) return false;
            return isLarge({ w: parseFloat(px[0]), h: parseFloat(px[1]) });
          }

          function pseudoCovers(style, el) {
            if (isLarge({ w: parseFloat(style.width) || 0, h: parseFloat(style.height) || 0 })) return true;
            return style.top === "0px" && style.right === "0px" && style.bottom === "0px" && style.left === "0px" && isLarge(box(el));
          }

          function crowded(el) {
            if (!coversViewport(el)) return false;
            const text = (el.innerText || "").replace(/\s+/g, " ").trim();
            if (text.length > 80) return true;
            return el.querySelectorAll("input, textarea, select, button, a, nav, header, main").length > 6;
          }

          function hasSnapshotBackground(el) {
            if (el === document.documentElement || el === document.body || crowded(el)) return false;
            try {
              const own = backgroundLooksLikePhoto(getComputedStyle(el));
              const before = getComputedStyle(el, "::before");
              const after = getComputedStyle(el, "::after");
              const pseudo = (backgroundLooksLikePhoto(before) && pseudoCovers(before, el))
                || (backgroundLooksLikePhoto(after) && pseudoCovers(after, el));
              if (!own && !pseudo) return false;
              const text = (el.innerText || "").replace(/\s+/g, " ").trim();
              return text.length <= 80;
            } catch (e) { return false; }
          }

          function ancestorRestored(el) {
            let node = el.parentElement || null;
            if (!node && el.getRootNode) {
              const root = el.getRootNode();
              node = root && root.host ? root.host : null;
            }
            while (node) {
              if (node.classList && node.classList.contains(photoClass)) return true;
              if (node.dataset && node.dataset.doorstepPhoto === "1") return true;
              const root = node.getRootNode ? node.getRootNode() : null;
              node = node.parentElement || (root && root.host ? root.host : null);
            }
            return false;
          }

          function containsPicture(el) {
            const nodes = el.querySelectorAll("video, canvas, img, image, embed, object");
            for (let i = 0; i < nodes.length; i++) {
              if (isPicture(nodes[i])) return true;
            }
            return false;
          }

          function isPicture(el) {
            const tag = (el.tagName || "").toUpperCase();
            const size = box(el);
            if (tag === "VIDEO" || tag === "EMBED" || tag === "OBJECT")
              return size.w >= 16 && size.h >= 16;
            if (tag === "IMG" || tag === "IMAGE" || tag === "CANVAS")
              return isLarge(size);
            return false;
          }

          function isPhoto(el) {
            if (!el || el.nodeType !== 1 || el.id === "doorstep-appearance" || ancestorRestored(el)) return false;
            if (isPicture(el)) return true;
            if (!isLarge(box(el)) || containsPicture(el)) return false;
            return hasSnapshotBackground(el);
          }

          function paint(el, on) {
            const root = el.getRootNode ? el.getRootNode() : null;
            if (root && root.host) {
              if ((el.dataset.doorstepPhoto === "1") === on) return;
              if (on) {
                el.style.setProperty("filter", reverse, "important");
                el.dataset.doorstepPhoto = "1";
              } else {
                el.style.removeProperty("filter");
                delete el.dataset.doorstepPhoto;
              }
              return;
            }

            if (el.classList.contains(photoClass) === on) return;
            if (on) el.classList.add(photoClass);
            else el.classList.remove(photoClass);
          }

          function consider(el) {
            try {
              paint(el, isPhoto(el));
              const tag = el.tagName;
              if ((tag === "IMG" || tag === "VIDEO" || tag === "CANVAS") && el.dataset.doorstepWatch !== "1") {
                el.dataset.doorstepWatch = "1";
                const redo = function () { paint(el, isPhoto(el)); };
                if (tag === "IMG" && !el.complete)
                  el.addEventListener("load", redo, { once: true });
                if (tag === "VIDEO") {
                  el.addEventListener("loadedmetadata", redo);
                  el.addEventListener("playing", redo);
                }
                if (window.ResizeObserver)
                  new ResizeObserver(redo).observe(el);
              }
            } catch (e) {}
          }

          function walk(root) {
            if (!root || !root.querySelectorAll) return;
            observe(root);
            const nodes = root.querySelectorAll("img, image, video, canvas, object, embed, div, span, a, button, i, figure, section, article");
            for (let i = 0; i < nodes.length; i++) {
              consider(nodes[i]);
              if (nodes[i].shadowRoot) walk(nodes[i].shadowRoot);
            }
          }

          function observe(root) {
            if (!root || seenRoots.has(root)) return;
            seenRoots.add(root);
            new MutationObserver(schedule).observe(root, {
              subtree: true,
              childList: true,
              attributes: true,
              attributeFilter: ["src", "srcset", "style", "class"]
            });
          }

          function schedule() {
            if (scheduled) return;
            scheduled = true;
            setTimeout(function () {
              scheduled = false;
              if (document.documentElement) {
                ensureStyle();
                try { walk(document); } catch (e) {}
              }
            }, 200);
          }

          function startScan() {
            schedule();
            window.addEventListener("resize", schedule);
            [500, 1500, 4000].forEach(function (ms) { setTimeout(schedule, ms); });
          }

          function boot() {
            if (!window.__doorstepHooks) {
              window.__doorstepHooks = true;
              document.addEventListener("DOMContentLoaded", boot);
              window.addEventListener("load", boot);
            }
            if (!ensureStyle()) return;
            if (window.__doorstepScan) return;
            window.__doorstepScan = true;
            try { startScan(); } catch (e) {}
          }

          boot();
        })();
        """;
}
