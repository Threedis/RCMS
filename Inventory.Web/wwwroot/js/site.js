/*
 * Enterprise Inventory Management System — shared client-side helpers.
 *
 * Everything the screens need lives on one global object, `inv`:
 *   - AJAX that always sends the anti-forgery token and always returns the
 *     same envelope shape, so no screen has to handle transport concerns;
 *   - DataTables and Select2 wiring with the project's defaults applied once;
 *   - toasts, confirmations and form helpers.
 *
 * Deliberately written as an IIFE over jQuery rather than a framework: the
 * application is server rendered, and this keeps the payload small.
 */
(function (window, $) {
    'use strict';

    const inv = {};

    // -------------------------------------------------------------------------
    // Formatting
    // -------------------------------------------------------------------------
    const money = new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const quantity = new Intl.NumberFormat('en-IN', { minimumFractionDigits: 0, maximumFractionDigits: 3 });
    const integer = new Intl.NumberFormat('en-IN');

    inv.format = {
        money: value => (value === null || value === undefined) ? '' : money.format(value),
        currency: value => (value === null || value === undefined) ? '' : '₹' + money.format(value),
        quantity: value => (value === null || value === undefined) ? '' : quantity.format(value),
        integer: value => (value === null || value === undefined) ? '' : integer.format(value),

        date: function (value) {
            if (!value) { return ''; }
            const parsed = new Date(value);
            if (isNaN(parsed)) { return ''; }
            return parsed.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
        },

        dateTime: function (value) {
            if (!value) { return ''; }
            const parsed = new Date(value);
            if (isNaN(parsed)) { return ''; }
            return parsed.toLocaleString('en-GB', {
                day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit'
            });
        }
    };

    /*
     * Escapes text before it is written into a DataTables cell.
     * Every render function below routes user data through this, so a value that
     * happens to contain markup is displayed, never executed.
     */
    inv.escape = function (value) {
        if (value === null || value === undefined) { return ''; }
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    };

    // -------------------------------------------------------------------------
    // DataTables cell renderers
    // -------------------------------------------------------------------------
    inv.render = {
        text: value => inv.escape(value),
        money: value => `<span class="text-end d-block">${inv.format.money(value)}</span>`,
        quantity: value => `<span class="text-end d-block">${inv.format.quantity(value)}</span>`,
        integer: value => `<span class="text-end d-block">${inv.format.integer(value)}</span>`,
        date: value => inv.format.date(value),
        dateTime: value => inv.format.dateTime(value),

        status: function (isActive) {
            return isActive
                ? '<span class="badge bg-success-subtle text-success-emphasis">Active</span>'
                : '<span class="badge bg-secondary-subtle text-secondary-emphasis">Inactive</span>';
        },

        documentStatus: function (statusName) {
            const map = {
                'Draft': 'secondary',
                'Pending Approval': 'warning',
                'Approved': 'success',
                'Rejected': 'danger',
                'Issued': 'info',
                'Closed': 'dark',
                'Cancelled': 'danger'
            };
            const css = map[statusName] || 'secondary';
            return `<span class="badge bg-${css}-subtle text-${css}-emphasis">${inv.escape(statusName)}</span>`;
        },

        boolean: value => value
            ? '<i class="bi bi-check-circle-fill text-success"></i>'
            : '<i class="bi bi-dash text-muted"></i>',

        lowStock: function (isLow) {
            return isLow
                ? '<span class="badge bg-danger-subtle text-danger-emphasis"><i class="bi bi-exclamation-triangle me-1"></i>Low</span>'
                : '';
        }
    };

    // -------------------------------------------------------------------------
    // AJAX
    // -------------------------------------------------------------------------

    /** Reads the anti-forgery token rendered into every page by the layout. */
    function antiForgeryToken() {
        return $('input[name="__RequestVerificationToken"]').first().val();
    }

    // Attach the token and the AJAX marker to every request this page makes.
    $.ajaxSetup({
        beforeSend: function (xhr, settings) {
            xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');

            if (!/^(GET|HEAD|OPTIONS|TRACE)$/i.test(settings.type)) {
                xhr.setRequestHeader('RequestVerificationToken', antiForgeryToken());
            }
        }
    });

    function handleFailure(jqXhr) {
        if (jqXhr.status === 401) {
            window.location.href = '/Account/Login';
            return { success: false, message: 'Your session has expired. Please sign in again.' };
        }

        if (jqXhr.status === 403) {
            return { success: false, message: 'You do not have permission to perform this action.' };
        }

        if (jqXhr.responseJSON && typeof jqXhr.responseJSON.success !== 'undefined') {
            return jqXhr.responseJSON;
        }

        return { success: false, message: 'The request could not be completed. Please try again.' };
    }

    inv.get = function (url, data) {
        return $.ajax({ url: url, type: 'GET', data: data, dataType: 'json' })
            .then(response => response, (jqXhr) => handleFailure(jqXhr));
    };

    inv.post = function (url, data) {
        return $.ajax({ url: url, type: 'POST', data: data, dataType: 'json' })
            .then(response => response, (jqXhr) => handleFailure(jqXhr));
    };

    /** Posts a form, including the anti-forgery token it already contains. */
    inv.postForm = function (url, $form) {
        return inv.post(url, $form.serialize());
    };

    // -------------------------------------------------------------------------
    // Toasts and confirmation
    // -------------------------------------------------------------------------
    inv.toast = function (message, type) {
        type = type || 'info';

        const icons = {
            success: 'bi-check-circle-fill',
            danger: 'bi-exclamation-octagon-fill',
            warning: 'bi-exclamation-triangle-fill',
            info: 'bi-info-circle-fill'
        };

        const $toast = $(`
            <div class="toast align-items-center border-0 text-bg-${type}" role="alert"
                 aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">
                        <i class="bi ${icons[type] || icons.info} me-2"></i>${inv.escape(message)}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto"
                            data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>`);

        $('#toastContainer').append($toast);

        const toast = new bootstrap.Toast($toast[0], { delay: type === 'danger' ? 8000 : 4000 });
        toast.show();

        $toast.on('hidden.bs.toast', () => $toast.remove());
    };

    /** Promise-based confirmation dialog; resolves true when the user confirms. */
    inv.confirm = function (title, message) {
        return new Promise(function (resolve) {
            $('#confirmModalTitle').text(title);
            $('#confirmModalBody').text(message);

            const modal = new bootstrap.Modal('#confirmModal');
            let confirmed = false;

            $('#confirmModalOk').off('click').on('click', function () {
                confirmed = true;
                modal.hide();
            });

            $('#confirmModal').off('hidden.bs.modal').on('hidden.bs.modal', function () {
                resolve(confirmed);
            });

            modal.show();
        });
    };

    // -------------------------------------------------------------------------
    // Forms
    // -------------------------------------------------------------------------

    /** Populates a form from a DTO, matching input names to property names. */
    inv.fillForm = function ($form, data) {
        if (!data) { return; }

        Object.keys(data).forEach(function (key) {
            const value = data[key];
            const $field = $form.find(`[name="${key}"]`).not('[type="hidden"][value="false"]');

            if (!$field.length) { return; }

            if ($field.is(':checkbox')) {
                $field.prop('checked', value === true);
            } else if ($field.is('select')) {
                $field.val(value === null ? '' : value);
                if ($field.hasClass('select2')) { $field.trigger('change.select2'); }
            } else if ($field.attr('type') === 'date' && value) {
                $field.val(String(value).substring(0, 10));
            } else {
                $field.val(value === null ? '' : value);
            }
        });

        // Hidden Id fields are matched by id, since the name filter above skips them.
        if (typeof data.id !== 'undefined') { $form.find('#Id').val(data.id); }
    };

    /** HTML5 validation plus the project's own styling. */
    inv.validateForm = function ($form) {
        const form = $form[0];

        if (form.checkValidity()) {
            $form.removeClass('was-validated');
            return true;
        }

        $form.addClass('was-validated');
        const $first = $form.find(':invalid').first();

        if ($first.length) {
            $first.trigger('focus');
        }

        return false;
    };

    inv.clearValidation = function ($form) {
        $form.removeClass('was-validated');
        $form.find('.is-invalid').removeClass('is-invalid');
        $form.find('[data-valmsg-for]').text('');
    };

    /** Renders a failed server response next to the fields it refers to. */
    inv.showErrors = function ($form, response, summarySelector) {
        inv.clearValidation($form);

        const messages = [];

        if (response.errors) {
            Object.keys(response.errors).forEach(function (key) {
                const list = response.errors[key];
                const $field = $form.find(`[name="${key}"]`);

                if ($field.length) {
                    $field.addClass('is-invalid');
                    $form.find(`[data-valmsg-for="${key}"]`).text(list[0]);
                } else {
                    list.forEach(m => messages.push(m));
                }
            });
        }

        if (!messages.length && response.message) {
            messages.push(response.message);
        }

        if (summarySelector && messages.length) {
            $(summarySelector)
                .removeClass('d-none')
                .html('<ul class="mb-0 ps-3">' +
                    messages.map(m => `<li>${inv.escape(m)}</li>`).join('') + '</ul>');
        } else if (messages.length) {
            inv.toast(messages[0], 'danger');
        }
    };

    /** Serialises the shared filter panel into a plain object. */
    inv.filterValues = function () {
        const values = {};

        $('#filterForm').find('input, select').each(function () {
            const name = this.name;
            const value = $(this).val();

            if (name && value !== '' && value !== null) {
                values[name] = value;
            }
        });

        return values;
    };

    // -------------------------------------------------------------------------
    // Grids
    // -------------------------------------------------------------------------

    /**
     * Creates a server-side DataTable with the project's defaults.
     * `options.data` may be a function, evaluated on every draw, so a grid can
     * pick up the current filter panel values without being rebuilt.
     */
    inv.grid = function (selector, options) {
        const settings = $.extend(true, {
            processing: true,
            serverSide: true,
            responsive: true,
            autoWidth: false,
            pageLength: 25,
            lengthMenu: [[10, 25, 50, 100], [10, 25, 50, 100]],
            language: {
                processing: '<div class="spinner-border spinner-border-sm text-primary"></div>',
                emptyTable: 'No records found.',
                zeroRecords: 'No records match the current filter.',
                info: 'Showing _START_ to _END_ of _TOTAL_',
                infoEmpty: 'No records',
                infoFiltered: '(filtered from _MAX_)',
                lengthMenu: '_MENU_ rows',
                search: '',
                searchPlaceholder: 'Search…',
                paginate: { first: '«', last: '»', next: '›', previous: '‹' }
            },
            dom: "<'row align-items-center mb-2'<'col-sm-6'l><'col-sm-6'f>>" +
                 "<'row'<'col-12'tr>>" +
                 "<'row align-items-center mt-2'<'col-sm-5'i><'col-sm-7'p>>",
            ajax: {
                url: options.url,
                type: 'POST',
                data: function (d) {
                    const extra = typeof options.data === 'function' ? options.data() : (options.data || {});
                    return $.extend({}, d, extra);
                },
                error: function (jqXhr) {
                    const response = handleFailure(jqXhr);
                    inv.toast(response.message, 'danger');
                }
            }
        }, options);

        // `url` and `data` were folded into `ajax`; leave them out of DataTables.
        delete settings.url;
        delete settings.data;

        return $(selector).DataTable(settings);
    };

    // -------------------------------------------------------------------------
    // Select2
    // -------------------------------------------------------------------------
    inv.select2 = function (selector, options) {
        return $(selector).select2($.extend({
            theme: 'bootstrap-5',
            width: '100%',
            allowClear: true,
            placeholder: $(selector).data('placeholder') || 'Select…'
        }, options || {}));
    };

    /** Item picker used on the GRN and issue line grids. */
    inv.itemPicker = function (selector, warehouseSelector) {
        return $(selector).select2({
            theme: 'bootstrap-5',
            width: '100%',
            placeholder: 'Search by name, code or part number…',
            minimumInputLength: 2,
            ajax: {
                url: '/Item/Search',
                dataType: 'json',
                delay: 300,
                data: function (params) {
                    return {
                        term: params.term,
                        warehouseId: $(warehouseSelector).val() || 0
                    };
                },
                processResults: function (data) {
                    return { results: data.results || [] };
                }
            },
            templateResult: function (item) {
                if (!item.id) { return item.text; }

                return $(`
                    <div class="d-flex justify-content-between">
                        <div>
                            <div class="fw-semibold">${inv.escape(item.itemName)}</div>
                            <small class="text-muted">${inv.escape(item.itemCode)}${item.partNumber ? ' · ' + inv.escape(item.partNumber) : ''}</small>
                        </div>
                        <div class="text-end">
                            <div class="small">Avail: <strong>${inv.format.quantity(item.availableStock)}</strong></div>
                            <small class="text-muted">${inv.escape(item.unitSymbol || '')}</small>
                        </div>
                    </div>`);
            }
        });
    };

    // -------------------------------------------------------------------------
    // Notifications poller
    // -------------------------------------------------------------------------
    inv.notifications = {
        refresh: function () {
            inv.get('/Notification/Latest', { unreadOnly: true }).then(function (response) {
                if (!response.success) { return; }

                const unread = response.data.unread || 0;
                const $badge = $('#notificationCount');

                $badge.text(unread > 99 ? '99+' : unread).toggleClass('d-none', unread === 0);

                const items = response.data.items || [];
                const $list = $('#notificationList');

                if (!items.length) {
                    $list.html('<div class="text-center text-muted py-4 small">You are all caught up.</div>');
                    return;
                }

                $list.html(items.map(function (n) {
                    const href = n.actionUrl ? inv.escape(n.actionUrl) : '#';
                    return `
                        <a class="dropdown-item notification-item d-flex gap-2 ${n.isRead ? '' : 'unread'}"
                           href="${href}" data-id="${n.id}">
                            <span class="badge bg-${n.badgeClass}-subtle text-${n.badgeClass}-emphasis align-self-start">
                                <i class="bi bi-dot"></i>
                            </span>
                            <span class="flex-grow-1">
                                <span class="d-block fw-semibold small">${inv.escape(n.title)}</span>
                                <span class="d-block text-muted small text-wrap">${inv.escape(n.message)}</span>
                                <span class="d-block text-muted" style="font-size:.7rem;">${inv.escape(n.timeAgo)}</span>
                            </span>
                        </a>`;
                }).join(''));
            });
        },

        start: function (intervalSeconds) {
            inv.notifications.refresh();
            window.setInterval(inv.notifications.refresh, (intervalSeconds || 60) * 1000);
        }
    };

    // -------------------------------------------------------------------------
    // Charts
    // -------------------------------------------------------------------------

    /*
     * A single categorical palette used by every chart, so the same category is
     * the same colour on every screen. Ordered for contrast at small sizes.
     */
    inv.palette = [
        '#0d6efd', '#20c997', '#fd7e14', '#6f42c1', '#d63384',
        '#198754', '#0dcaf0', '#ffc107', '#dc3545', '#6c757d'
    ];

    inv.chart = {
        defaults: function () {
            Chart.defaults.font.family =
                "system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif";
            Chart.defaults.font.size = 12;
            Chart.defaults.color = '#495057';
            Chart.defaults.plugins.legend.labels.usePointStyle = true;
            Chart.defaults.plugins.legend.labels.boxWidth = 8;
        },

        doughnut: function (canvasId, labels, values, title) {
            const canvas = document.getElementById(canvasId);
            if (!canvas) { return null; }

            return new Chart(canvas, {
                type: 'doughnut',
                data: {
                    labels: labels,
                    datasets: [{
                        data: values,
                        backgroundColor: inv.palette,
                        borderWidth: 2,
                        borderColor: '#fff'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    cutout: '62%',
                    plugins: {
                        legend: { position: 'right' },
                        title: { display: !!title, text: title },
                        tooltip: {
                            callbacks: {
                                label: ctx => `${ctx.label}: ${inv.format.currency(ctx.parsed)}`
                            }
                        }
                    }
                }
            });
        },

        bar: function (canvasId, labels, values, label, horizontal) {
            const canvas = document.getElementById(canvasId);
            if (!canvas) { return null; }

            return new Chart(canvas, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: label,
                        data: values,
                        backgroundColor: inv.palette[0],
                        borderRadius: 4,
                        maxBarThickness: 36
                    }]
                },
                options: {
                    indexAxis: horizontal ? 'y' : 'x',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { grid: { display: horizontal } },
                        y: { grid: { display: !horizontal }, beginAtZero: true }
                    }
                }
            });
        },

        line: function (canvasId, labels, datasets) {
            const canvas = document.getElementById(canvasId);
            if (!canvas) { return null; }

            return new Chart(canvas, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: datasets.map(function (set, index) {
                        return {
                            label: set.label,
                            data: set.data,
                            borderColor: inv.palette[index % inv.palette.length],
                            backgroundColor: inv.palette[index % inv.palette.length] + '20',
                            fill: true,
                            tension: 0.35,
                            pointRadius: 3,
                            pointHoverRadius: 5,
                            borderWidth: 2
                        };
                    })
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: { position: 'top', align: 'end' },
                        tooltip: {
                            callbacks: {
                                label: ctx => `${ctx.dataset.label}: ${inv.format.currency(ctx.parsed.y)}`
                            }
                        }
                    },
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: { callback: value => inv.format.integer(value) }
                        }
                    }
                }
            });
        }
    };

    // -------------------------------------------------------------------------
    // Page bootstrap
    // -------------------------------------------------------------------------
    $(function () {
        // Sidebar on small screens.
        $('#sidebarToggle').on('click', () => $('body').addClass('sidebar-open'));
        $('#sidebarClose, #sidebarBackdrop').on('click', () => $('body').removeClass('sidebar-open'));

        // Any select marked .select2 gets the shared configuration.
        if ($.fn.select2) {
            inv.select2('.select2');
        }

        // Notification bell.
        if ($('#notificationBell').length) {
            inv.notifications.start(60);

            $('#markAllRead').on('click', function () {
                inv.post('/Notification/MarkAllRead').then(function (response) {
                    inv.toast(response.message, response.success ? 'success' : 'danger');
                    inv.notifications.refresh();
                });
            });

            $('#notificationList').on('click', '.notification-item', function () {
                inv.post('/Notification/MarkRead', { id: $(this).data('id') });
            });
        }

        // Pending approval badge in the sidebar.
        if ($('#sidebarApprovalCount').length) {
            inv.get('/Approval/Count').then(function (response) {
                if (response.success && response.data.count > 0) {
                    $('#sidebarApprovalCount').text(response.data.count).removeClass('d-none');
                }
            });
        }

        if (window.Chart) {
            inv.chart.defaults();
        }

        // Warn once before leaving a form with unsaved changes.
        $('form[data-track-changes]').each(function () {
            const $form = $(this);
            let dirty = false;

            $form.on('change input', ':input', () => { dirty = true; });
            $form.on('submit', () => { dirty = false; });

            $(window).on('beforeunload', function () {
                if (dirty) { return 'You have unsaved changes.'; }
            });
        });
    });

    window.inv = inv;
})(window, jQuery);
