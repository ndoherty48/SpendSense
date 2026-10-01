/*
 * Theme bootstrap. Loaded synchronously in <head> so <html data-theme> is set before first paint.
 *
 * The app's ThemeService (C#) is authoritative and calls ssTheme.apply() once Blazor boots and on every
 * change. Until then we read a localStorage mirror of the chosen mode; in System mode (or with no mirror)
 * we follow the webview's prefers-color-scheme, so a stale mirror never overrides an OS theme change.
 */
(function () {
    var root = document.documentElement;

    function setTheme(theme) {
        root.setAttribute('data-theme', theme);
        root.style.colorScheme = theme;
    }

    function systemTheme() {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
    }

    var mode = null;
    try { mode = localStorage.getItem('ss-theme-mode'); } catch (e) { /* storage unavailable */ }
    setTheme(mode === 'light' || mode === 'dark' ? mode : systemTheme());

    window.ssTheme = {
        // mode: 'system' | 'light' | 'dark'; isDark: the theme actually in effect.
        apply: function (mode, isDark) {
            try { localStorage.setItem('ss-theme-mode', mode); } catch (e) { /* storage unavailable */ }
            setTheme(isDark ? 'dark' : 'light');
        }
    };
})();
