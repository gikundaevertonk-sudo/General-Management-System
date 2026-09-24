/*
 * Light/dark theme switching.
 *
 * Two halves that must stay in step:
 *
 *  1. A tiny inline snippet in _Layout's <head> sets data-bs-theme BEFORE the
 *     first paint. It has to be inline and in the head - loading it as an
 *     external file means the browser paints the default (light) theme first
 *     and a dark-mode user gets a white flash on every navigation. If you
 *     change the storage key or attribute here, change it there too.
 *
 *  2. This file, loaded at the end of the body, wires up the toggle.
 *
 * The preference is stored per browser in localStorage rather than against the
 * user account: it is a display choice, not business data, and storing it
 * locally means the choice survives sign-out and applies to the login page,
 * where nobody is signed in to have a preference.
 */
(function () {
    'use strict';

    var STORAGE_KEY = 'gms-theme';

    function systemTheme() {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches
            ? 'dark'
            : 'light';
    }

    function stored() {
        try {
            return localStorage.getItem(STORAGE_KEY);
        } catch (e) {
            // Private mode / storage disabled: fall back to the system setting.
            return null;
        }
    }

    function apply(theme) {
        document.documentElement.setAttribute('data-bs-theme', theme);
        document.querySelectorAll('.theme-toggle').forEach(function (btn) {
            btn.setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');
        });
    }

    function save(theme) {
        try {
            localStorage.setItem(STORAGE_KEY, theme);
        } catch (e) {
            // Non-fatal: the theme still applies for this page view.
        }
    }

    function current() {
        return document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'dark' : 'light';
    }

    document.addEventListener('DOMContentLoaded', function () {
        apply(current());

        document.querySelectorAll('.theme-toggle').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var next = current() === 'dark' ? 'light' : 'dark';
                apply(next);
                save(next);
            });
        });
    });

    // Follow the OS setting as it changes, but only while the user has not made
    // an explicit choice - an explicit choice should stick.
    if (window.matchMedia) {
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function () {
            if (!stored()) {
                apply(systemTheme());
            }
        });
    }
})();
