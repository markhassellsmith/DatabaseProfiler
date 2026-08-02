// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(() => {
  const showSchemaPanel = (hash) => {
    if (!hash || !hash.startsWith('#schema-')) {
      return;
    }

    const panel = document.querySelector(hash);
    if (!(panel instanceof HTMLElement) || !panel.classList.contains('schema-browser-panel')) {
      return;
    }

    const collapse = panel.querySelector('.accordion-collapse');
    if (!(collapse instanceof HTMLElement)) {
      return;
    }

    const instance = bootstrap.Collapse.getOrCreateInstance(collapse, { toggle: false });
    const scrollToPanel = () => panel.scrollIntoView({ behavior: 'smooth', block: 'start' });

    if (collapse.classList.contains('show')) {
      scrollToPanel();
      return;
    }

    collapse.addEventListener('shown.bs.collapse', scrollToPanel, { once: true });
    instance.show();
  };

  document.addEventListener('DOMContentLoaded', () => {
    const syncProfilingTableScrollbars = () => {
      const topScrollbar = document.querySelector('.profiling-table-scrollbar');
      const tableContainer = document.querySelector('.profiling-page .table-responsive');

      if (!(topScrollbar instanceof HTMLElement) || !(tableContainer instanceof HTMLElement)) {
        return;
      }

      const syncFromTop = () => {
        tableContainer.scrollLeft = topScrollbar.scrollLeft;
      };

      const syncFromTable = () => {
        topScrollbar.scrollLeft = tableContainer.scrollLeft;
      };

      topScrollbar.addEventListener('scroll', syncFromTop);
      tableContainer.addEventListener('scroll', syncFromTable);

      syncFromTable();
    };

    document.querySelectorAll('a[data-schema-panel-link]').forEach((link) => {
      link.addEventListener('click', (event) => {
        event.preventDefault();

        const href = link.getAttribute('href');
        if (!href) {
          return;
        }

        if (window.location.hash === href) {
          showSchemaPanel(href);
          return;
        }

        window.location.hash = href;
      });
    });

    document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach((element) => {
      if (element instanceof HTMLElement) {
        bootstrap.Tooltip.getOrCreateInstance(element);
      }
    });

    showSchemaPanel(window.location.hash);
    syncProfilingTableScrollbars();
  });

  window.addEventListener('hashchange', () => {
    showSchemaPanel(window.location.hash);
  });
})();
