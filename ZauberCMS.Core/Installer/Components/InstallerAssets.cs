namespace ZauberCMS.Core.Installer.Components;

/// <summary>
/// The installer's stylesheet and script. They are written into the page: the installer runs before the site has
/// a database, so it must not depend on static files, a CDN or anything else being in place.
/// </summary>
internal static class InstallerAssets
{
    public const string Css = """
        :root {
          --ink: #16132d;
          --ink-soft: #2a2552;
          --violet: #5a3fd4;
          --violet-deep: #4630ad;
          --gold: #f2b84b;
          --paper: #ffffff;
          --mist: #f5f3fc;
          --line: #d6d1e8;
          /* The edge of a control is all that shows it is there: 3:1 against the page */
          --edge: #857fa3;
          --text: #1d1a33;
          --muted: #5f5b78;
          --bad: #a8261f;
          --bad-wash: #fcecea;
          --good: #17694a;
          --good-wash: #e6f4ed;
          --serif: "Iowan Old Style", "Palatino Linotype", Palatino, "Book Antiqua", Georgia, serif;
          --sans: "Segoe UI Variable Text", "Segoe UI", system-ui, -apple-system, "Helvetica Neue", Arial, sans-serif;
        }
        *, *::before, *::after { box-sizing: border-box; }
        html { -webkit-text-size-adjust: 100%; }
        body {
          margin: 0;
          min-height: 100vh;
          display: grid;
          grid-template-columns: minmax(16rem, 21rem) 1fr;
          font: 1rem/1.55 var(--sans);
          color: var(--text);
          background: var(--paper);
        }
        [hidden] { display: none !important; }

        /* The rail: where you are in the sequence */
        .rail {
          background: var(--ink);
          color: #fff;
          padding: 2.75rem 2.25rem 2rem;
          display: flex;
          flex-direction: column;
        }
        .brand { display: flex; align-items: center; gap: .8rem; }
        .brand svg { width: 3.4rem; height: auto; fill: #fff; flex: none; }
        .brand span { font: 500 1.35rem/1 var(--serif); letter-spacing: .01em; }
        .steps { list-style: none; margin: 3.5rem 0 0; padding: 0; }
        .steps li {
          position: relative;
          padding: 0 0 1.9rem 2.1rem;
          color: rgba(255, 255, 255, .58);
          font-size: .98rem;
        }
        .steps li::before {
          content: "";
          position: absolute;
          left: .2rem; top: .42rem;
          width: .62rem; height: .62rem;
          border: 1.5px solid rgba(255, 255, 255, .45);
          border-radius: 50%;
        }
        .steps li::after {
          content: "";
          position: absolute;
          left: .48rem; top: 1.35rem; bottom: .2rem;
          width: 1px;
          background: rgba(255, 255, 255, .18);
        }
        .steps li:last-child { padding-bottom: 0; }
        .steps li:last-child::after { display: none; }
        .steps li[data-state="done"] { color: rgba(255, 255, 255, .82); }
        .steps li[data-state="done"]::before { background: #fff; border-color: #fff; }
        .steps li[data-state="current"] { color: #fff; font-weight: 600; }
        /* The star from the tip of the wand marks the step you are on */
        .steps li[data-state="current"]::before {
          left: -.14rem; top: .08rem;
          width: 1.3rem; height: 1.3rem;
          border: 0; border-radius: 0;
          background: var(--gold);
          clip-path: polygon(50% 0, 61% 39%, 100% 50%, 61% 61%, 50% 100%, 39% 61%, 0 50%, 39% 39%);
        }
        .steps small { display: block; font-weight: 400; font-size: .82rem; color: rgba(255, 255, 255, .58); }
        .rail-note { margin-top: auto; padding-top: 2.5rem; font-size: .82rem; line-height: 1.5; color: rgba(255, 255, 255, .6); }
        .rail-note p { margin: 0 0 .6rem; }

        /* The page */
        main { padding: clamp(2rem, 7vw, 5.5rem) clamp(1.25rem, 6vw, 5rem); }
        .sheet { max-width: 35rem; }
        h1 { font: 500 clamp(1.75rem, 3.2vw, 2.35rem)/1.15 var(--serif); margin: 0 0 .75rem; letter-spacing: -.005em; }
        h1:focus { outline: none; }
        .lead { color: var(--muted); margin: 0 0 2rem; max-width: 32rem; }
        p { margin: 0 0 1rem; overflow-wrap: anywhere; }
        .notice { overflow-wrap: anywhere; }
        code { font: .88em ui-monospace, "Cascadia Mono", Consolas, monospace; background: var(--mist); border-radius: 4px; padding: .08em .3em; }

        .field { margin: 0 0 1.25rem; }
        .field > label, .legend { display: block; font-weight: 600; font-size: .92rem; margin: 0 0 .35rem; }
        .hint { display: block; color: var(--muted); font-size: .86rem; margin-top: .35rem; }
        input[type="text"], input[type="email"], input[type="password"], input[type="number"], select, textarea {
          width: 100%;
          font: inherit;
          color: inherit;
          background: var(--paper);
          border: 1px solid var(--edge);
          border-radius: 6px;
          padding: .68rem .8rem;
        }
        textarea { min-height: 5.5rem; resize: vertical; font-family: ui-monospace, "Cascadia Mono", Consolas, monospace; font-size: .88rem; }
        input:hover, select:hover, textarea:hover { border-color: var(--violet); }
        :focus-visible { outline: 2px solid var(--violet); outline-offset: 2px; }
        .pair { display: grid; grid-template-columns: 1fr 8rem; gap: 1rem; }
        .with-button { display: grid; grid-template-columns: 1fr auto; gap: .5rem; }

        fieldset { border: 0; padding: 0; margin: 0 0 1.5rem; min-width: 0; }
        legend { padding: 0; }
        .choices { display: grid; gap: .6rem; }
        .choices.across { grid-template-columns: repeat(3, 1fr); }
        .choice { position: relative; display: block; }
        .choice input { position: absolute; opacity: 0; inset: 0; width: 100%; height: 100%; margin: 0; cursor: pointer; }
        .choice span {
          display: block;
          height: 100%;
          border: 1px solid var(--edge);
          border-radius: 8px;
          padding: .8rem .95rem;
          font-size: .88rem;
          color: var(--muted);
        }
        .choice b { display: block; color: var(--text); font-size: 1rem; margin-bottom: .15rem; }
        .choice input:checked + span { border-color: var(--violet); box-shadow: inset 0 0 0 1px var(--violet); background: var(--mist); }
        .choice input:focus-visible + span { outline: 2px solid var(--violet); outline-offset: 2px; }
        @media (forced-colors: active) {
          /* Colours are replaced by the system's: say "selected" with something that survives */
          .choice input:checked + span { outline: 3px solid Highlight; outline-offset: -3px; }
        }

        .check { display: grid; grid-template-columns: auto 1fr; gap: .1rem .65rem; align-items: start; margin: 0 0 1.25rem; }
        .check input { width: 1.1rem; height: 1.1rem; margin: .22rem 0 0; accent-color: var(--violet); }
        .check label { font-weight: 600; font-size: .95rem; }
        .check .hint { grid-column: 2; margin-top: 0; }

        /* Which database fields show follows the choice above them, with or without script. A browser
           without :has() shows them all, which still works. */
        @supports selector(:has(*)) {
          .for-sqlite, .for-sqlserver, .for-postgresql, .for-server, .for-string { display: none; }
          form:has(#db-sqlite:checked) .for-sqlite,
          form:has(#db-sqlserver:checked) .for-sqlserver,
          form:has(#db-postgresql:checked) .for-postgresql,
          form:has(#db-sqlserver:checked) .for-server,
          form:has(#db-postgresql:checked) .for-server,
          form:has(#db-string:checked) .for-string { display: block; }
          form:has(#db-sqlserver:checked) .pair.for-server,
          form:has(#db-postgresql:checked) .pair.for-server { display: grid; }
          form:has(#db-sqlserver:checked) .check.for-sqlserver { display: grid; }
          form:has(#db-string:checked) .for-fields { display: none !important; }
          form:has(#db-integrated:checked):has(#db-sqlserver:checked) .for-login { display: none; }
          form:has(#db-sqlserver:checked) .pair.for-server { grid-template-columns: 1fr; }
          form:has(#db-sqlserver:checked) .for-port { display: none; }
        }

        .actions { display: flex; flex-wrap: wrap; gap: .75rem; align-items: center; margin-top: 2rem; }
        button, .button {
          font: 600 1rem/1.2 var(--sans);
          border-radius: 6px;
          padding: .78rem 1.3rem;
          border: 1px solid var(--violet);
          background: var(--violet);
          color: #fff;
          cursor: pointer;
          text-decoration: none;
          display: inline-block;
        }
        button:hover, .button:hover { background: var(--violet-deep); border-color: var(--violet-deep); }
        button.quiet { background: transparent; color: var(--violet-deep); border-color: var(--edge); }
        button.quiet:hover { background: var(--mist); border-color: var(--violet); }
        button.plain { background: transparent; border-color: transparent; color: var(--muted); padding-left: .25rem; padding-right: .25rem; }
        button.plain:hover { color: var(--text); background: transparent; }
        button[aria-busy="true"] { opacity: .7; cursor: progress; }

        .notice { border-radius: 8px; padding: .85rem 1rem; margin: 0 0 1.5rem; font-size: .95rem; border: 1px solid; }
        .notice ul { margin: 0; padding-left: 1.1rem; }
        .notice.bad { color: var(--bad); background: var(--bad-wash); border-color: #f2c4bf; }
        .notice.good { color: var(--good); background: var(--good-wash); border-color: #b9dfcb; }

        .review { margin: 0 0 1rem; border-top: 1px solid var(--line); }
        .review div { display: grid; grid-template-columns: 11rem 1fr; gap: 1rem; padding: .7rem 0; border-bottom: 1px solid var(--line); }
        .review dt { color: var(--muted); font-size: .92rem; }
        .review dd { margin: 0; font-weight: 600; overflow-wrap: anywhere; }
        pre {
          background: var(--ink);
          color: #f1eeff;
          border-radius: 8px;
          padding: 1rem 1.1rem;
          overflow-x: auto;
          font: .86rem/1.5 ui-monospace, "Cascadia Mono", Consolas, monospace;
          margin: 0 0 1.25rem;
        }

        /* Without script the whole form is one page: no stepping, no review */
        html:not(.js) [data-go], html:not(.js) [data-reveal], html:not(.js) [data-pane="review"] .review, html:not(.js) [data-pane="review"] .lead { display: none; }
        html:not(.js) .with-button { grid-template-columns: 1fr; }
        .notice:focus { outline: 2px solid currentColor; outline-offset: 2px; }
        html:not(.js) [data-pane] { margin-bottom: 3rem; }
        html:not(.js) [data-pane] .actions { margin-top: 0; }

        @media (max-width: 52rem) {
          body { display: block; }
          .rail { padding: 1.25rem 1.25rem 1.1rem; display: block; }
          .brand svg { width: 2.5rem; }
          .steps { margin: 1.1rem 0 0; display: flex; gap: 1.1rem; align-items: center; }
          .steps li { padding: 0 0 0 1.5rem; font-size: 0; min-height: 1.3rem; }
          .steps li::after { display: none; }
          .steps li[data-state="current"] { font-size: .95rem; }
          .steps small { display: none; }
          .rail-note { display: none; }
          .choices.across { grid-template-columns: 1fr; }
          .review div { grid-template-columns: 1fr; gap: .1rem; }
        }
        @media (prefers-reduced-motion: no-preference) {
          button, .button, input, select, .choice span { transition: background-color .12s, border-color .12s; }
        }
        """;

    public const string Script = """
        (function () {
          var root = document.documentElement;
          root.classList.add('js');

          // Show the password while typing it: there is no "confirm" field to catch a typo
          Array.prototype.forEach.call(document.querySelectorAll('[data-reveal]'), function (button) {
            button.addEventListener('click', function () {
              var input = document.getElementById(button.getAttribute('data-reveal'));
              var show = input.type === 'password';
              input.type = show ? 'text' : 'password';
              // The words are the page's (data-show, data-hide), in the installer's language
              button.textContent = button.getAttribute(show ? 'data-hide' : 'data-show');
            });
          });

          // One click is enough: creating a database takes a few seconds
          Array.prototype.forEach.call(document.querySelectorAll('form'), function (form) {
            form.addEventListener('submit', function (event) {
              var button = event.submitter;
              if (form.getAttribute('data-sent')) { event.preventDefault(); return; }
              form.setAttribute('data-sent', '1');
              if (button) { button.setAttribute('aria-busy', 'true'); }
            });
          });

          // Coming back with the browser's Back button must not leave a form that refuses to send
          window.addEventListener('pageshow', function () {
            Array.prototype.forEach.call(document.querySelectorAll('form[data-sent]'), function (form) {
              form.removeAttribute('data-sent');
              Array.prototype.forEach.call(form.querySelectorAll('[aria-busy]'), function (b) { b.removeAttribute('aria-busy'); });
            });
          });

          // A refused step: take a keyboard or screen-reader user to the reason
          var problem = document.querySelector('.notice.bad');
          if (problem) { problem.setAttribute('tabindex', '-1'); problem.focus(); }

          var form = document.querySelector('[data-installer]');
          if (!form) { return; }

          var panes = Array.prototype.slice.call(form.querySelectorAll('[data-pane]'));
          var rail = Array.prototype.slice.call(document.querySelectorAll('[data-rail]'));
          var current = 0;

          function indexOf(name) {
            for (var i = 0; i < panes.length; i++) { if (panes[i].getAttribute('data-pane') === name) { return i; } }
            return -1;
          }

          function valueOf(name) {
            var checked = form.querySelector('input[type="radio"][name="' + name + '"]:checked');
            if (checked) { return checked.getAttribute('data-label') || checked.value; }
            var field = form.elements[name];
            if (!field) { return ''; }
            if (field.tagName === 'SELECT') { return field.selectedIndex < 0 ? '' : field.options[field.selectedIndex].text; }
            return field.value;
          }

          function fillReview() {
            Array.prototype.forEach.call(form.querySelectorAll('[data-review]'), function (cell) {
              var name = cell.getAttribute('data-review');
              var field = form.elements[name];
              if (field && field.type === 'checkbox') {
                cell.textContent = field.checked ? cell.getAttribute('data-on') : cell.getAttribute('data-off');
              } else {
                cell.textContent = valueOf(name);
              }
            });
          }

          function show(index, focus) {
            current = index;
            panes.forEach(function (pane, i) { pane.hidden = i !== index; });
            rail.forEach(function (item) {
              var at = indexOf(item.getAttribute('data-rail'));
              var state = at < 0 || at < index ? 'done' : at === index ? 'current' : 'todo';
              item.setAttribute('data-state', state);
              if (state === 'current') { item.setAttribute('aria-current', 'step'); } else { item.removeAttribute('aria-current'); }
            });
            if (panes[index].getAttribute('data-pane') === 'review') { fillReview(); }
            if (focus) {
              var heading = panes[index].querySelector('h1');
              if (heading) { heading.setAttribute('tabindex', '-1'); heading.focus(); }
              window.scrollTo(0, 0);
            }
          }

          function paneIsValid(pane) {
            var fields = pane.querySelectorAll('input, select');
            for (var i = 0; i < fields.length; i++) {
              if (!fields[i].checkValidity()) { fields[i].reportValidity(); return false; }
            }
            return true;
          }

          form.addEventListener('click', function (event) {
            var button = event.target.closest('[data-go]');
            if (!button) { return; }
            event.preventDefault();
            if (button.getAttribute('data-go') === 'back') { show(Math.max(0, current - 1), true); }
            else if (paneIsValid(panes[current])) { show(Math.min(panes.length - 1, current + 1), true); }
          });

          // Enter in a field moves to the next step; only the last step sends the form
          form.addEventListener('keydown', function (event) {
            if (event.key !== 'Enter' || event.target.tagName !== 'INPUT' || current >= panes.length - 1) { return; }
            event.preventDefault();
            if (paneIsValid(panes[current])) { show(current + 1, true); }
          });

          show(Math.max(0, indexOf(form.getAttribute('data-start'))), false);
        })();
        """;
}
