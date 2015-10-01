// Custom status bar for Ace (aka Project Wunderbar)
var statusbar = {
    showFilename:
        function () {
            var filename;
            try {
                filename = viewModel.fileEdit.peek().name();
            }
            catch(e) {
                filename = 'Can not get filename. See console for details.';
                if (typeof console == 'object') {
                    console.log('Can not get filename: %s', e);
                }
            }
            finally {
                $('#statusbar').text(filename);
                $('#statusbar').removeClass('statusbar-red');
            }
        },
    reset:
        function () {
            $('#statusbar').text('');
            $('#statusbar').removeClass('statusbar-red');
            // Flag from ace-init.js
            contentHasChanged = false;
            // Clear search box
            if (editor.searchBox) {
                editor.searchBox.activeInput.value = '';
                editor.searchBox.hide();
            }
        },
    SavingChanges:
        function () {
            $('#statusbar').text('Saving changes...');
            $('#statusbar').prepend('<i class="glyphicon glyphicon-cloud-upload" style="margin-right: 6px"></i>');
         },
    FetchingChanges:
        function () {
            $('#statusbar').text('Fetching changes...');
            $('#statusbar').prepend('<i class="glyphicon glyphicon-cloud-download" style="margin-right: 6px"></i>');
        }
}

var copyProgressObjects = {};
var copyObjectsManager = {
    init: function() {
        copyProgressObjects = {};
    },
    addCopyStats: function (uri, loadedData, totalData
   )  { 
        if (copyProgressObjects[uri]) {
            if (loadedData === totalData) { 
                copyProgressObjects[uri].endDate = $.now();
            }
        } else {
            copyProgressObjects[uri] = {};
            copyProgressObjects[uri].startDate = $.now();
            //this is used for when copying multiple files in the same time so that i may stii have a coherent percentage
            copyProgressObjects[uri].transactionPackFinished = false; 
        }

        copyProgressObjects[uri].loadedData = loadedData;
        copyProgressObjects[uri].totalData = totalData;
    },
    getCopyStats: function () {
        return copyProgressObjects;
    },
    getCurrentPercentCompletion: function () {
        var currentTransfered = 0;
        var finalTransfered = 0;
        var foundItem = false;

        for (var key in copyProgressObjects) {
            var co = copyProgressObjects[key];
            if(co.transactionPackFinished === false) {
                foundItem = true;
                currentTransfered += co.loadedData;
                finalTransfered += co.totalData;
            }
        }

        var perc = 0;
        if (foundItem) {
            perc = parseInt((currentTransfered / finalTransfered) * 100);
        } else { // to avoid 0/0
            perc = 100;
        }

        if (perc === 100 && foundItem) { // if all transactions have finished & have some unmarked transaction pack, cancel it out
            for (var key in copyProgressObjects) {
                copyProgressObjects[key].transactionPackFinished = true;
            }
        }

        return perc;
    }
}

var statusbarObj = Object.create(statusbar);
copyObjectsManager.init();


$.connection.hub.url = appRoot + "api/filesystemhub";
var fileSystemHub = $.connection.fileSystemHub;
fileSystemHub.client.fileExplorerChanged = function () {
    window.viewModel.selected().fetchChildren(true);
};
$.connection.hub.start().done(function () {
    var Vfs = {
        getContent: function (item) {
            return $.ajax({
                url: item.href,
                dataType: "text"
            });
        },

        setContent: function (item, text) {
            return $.ajax({
                url: item.href.replace(/#/g, encodeURIComponent("#")),
                data: text,
                method: "PUT",
                xhr: function () {  // Custom XMLHttpRequest
                    var myXhr = $.ajaxSettings.xhr();
                    if (myXhr.upload) { // Check if upload property exists
                        myXhr.upload.addEventListener('progress', function (e) {
                            copyProgressHandlingFunction(e, item.href);
                        }, false); // For handling the progress of the upload
                    }
                    return myXhr;
                },
                processData: false,
                headers: {
                    "If-Match": "*"
                }
            });
        },

        getChildren: function (item) {
            return $.get(item.href);
        },

        createFolder: function (folder) {
            return $.ajax({
                url: folder.href.replace(/#/g, encodeURIComponent("#")) + "/",
                method: "PUT"
            });
        },

        addFiles: function (files, unzip) {
            return whenArray(
                $.map(files, function (item) {
                    var baseHref = unzip ? viewModel.selected().href.replace(/\/vfs\//, "/zip/") : viewModel.selected().href;
                    return Vfs.setContent({ href: (baseHref + (unzip ? "" : item.name)) }, item.contents);
                })
            );
        },

        deleteItems: function (item) {
            var url = item.href;

            if (item.mime === "inode/directory") {
                url += "?recursive=true";
            }

            return $.ajax({
                url: url,
                method: "DELETE",
                headers: {
                    "If-Match": "*"
                }
            });
        }
    };

    var MAX_VIEW_ITEMS = 200;

    var node = function (data, parent) {
        this.parent = parent;
        this.name = ko.observable(data.name);
        this.size = ko.observable(data.size ? (Math.ceil(data.size / 1024) + ' KB') : '');
        this.mime = data.mime || (data.type === "dir" && "inode/directory");
        this.isDirectory = ko.observable(this.mime === "inode/directory");
        this.href = data.href;
        this._href = ko.observable(this.href);
        this.modifiedTime = ((data.mtime && new Date(data.mtime)) || new Date()).toLocaleString();
        this.url = ko.observable(this.isDirectory() ? data.href.replace(/\/vfs\//, "/zip/") : data.href);
        this.path = ko.observable(data.path);
        this.children = ko.observableArray([]);
        this.editing = ko.observable(data.editing || false);
        this._fetchStatus;

        this.fetchChildren = function (force) {
            var that = this;

            if (!that._fetchStatus || (force && that._fetchStatus === 2)) {
                that._fetchStatus = 1;
                viewModel.processing(true);

                return Vfs.getChildren(that)
                .done(function (data) {
                    viewModel.processing(false);
                    var children = that.children;
                    children.removeAll();

                    // maxViewItems overridable by localStorage setting.
                    var maxViewItems = getLocalStorageSetting("maxViewItems", MAX_VIEW_ITEMS);
                    var folders = [];
                    var files = $.map(data, function (elem) {
                        if (elem.mime === "inode/shortcut") {
                            viewModel.specialDirs.push(new node(elem));
                        } else if (--maxViewItems > 0) {
                            if (elem.mime === "inode/directory") {
                                // track folders explicitly to avoid additional sort
                                folders.push(new node(elem, that));
                            } else {
                                return new node(elem, that);
                            }
                        }
                    });

                    // view display folders then files
                    children.push.apply(children, folders);
                    children.push.apply(children, files);

                    that._fetchStatus = 2;
                }).fail(showError).promise();
            } else {
                return $.Deferred().resolve().promise();
            }
        }
        this.deleteItem = function () {
            if (confirm("Are you sure you want to delete '" + this.name() + "'?")) {
                var that = this;
                viewModel.processing(true);
                Vfs.deleteItems(this).done(function () {
                    that.parent.children.remove(that);
                    if (viewModel.selected() === this) {
                        viewModel.selected(this.parent);
                    }
                    viewModel.processing(false);
                }).fail(showError);
            }
        }
        this.selectNode = function () {
            var that = this;
            return this.fetchChildren().pipe(function () {
                stashCurrentSelection(viewModel.selected());
                viewModel.selected(that);

                return $.Deferred().resolve();
            });
        };
        this.selectChild = function (descendantPath) {
            var that = this;
            return this.fetchChildren().pipe(function () {
                var childName = descendantPath.split(/\/|\\/)[0].toLowerCase(),
                    matches = $.grep(that.children(), function (elm) {
                        return elm.name().toLowerCase() === childName;
                    }),
                    deferred;
                if (matches && matches.length) {
                    var selectedChild = matches[0];
                    viewModel.selected(selectedChild);
                    if (descendantPath.length > childName.length) {
                        deferred = selectedChild.selectChild(descendantPath.substring(childName.length + 1));
                    }
                    selectedChild.fetchChildren();
                }

                return deferred || $.Deferred().resolve();
            });
        }

        this.selectParent = function () {
            var that = viewModel.selected();
            if (that.parent) {
                stashCurrentSelection(that);
                viewModel.selected(that.parent);
            }
        }

        this.editItem = function () {
            var that = this;
            // Blank out the editor before fetching new content
            viewModel.editText(null);
            statusbarObj.FetchingChanges();
            viewModel.fileEdit(this);
            if(this.mime === "text/xml")
            {
                Vfs.getContent(this)
                   .done(function (data) {
                       viewModel.editText(vkbeautify.xml(data));
                       statusbarObj.showFilename();
                       // Editor h-scroll workaround
                       editor.session.setScrollLeft(-1);
                   }).fail(showError);
            }
            else {
                Vfs.getContent(this)
                   .done(function (data) {
                       viewModel.editText(data);
                       statusbarObj.showFilename();
                       // Editor h-scroll workaround
                       editor.session.setScrollLeft(-1);
                   }).fail(showError);
            }
        }

        this.saveItem = function () {
            var text = viewModel.editText();
            statusbarObj.SavingChanges();
            Vfs.setContent(this, text)
                .done(function () {
                    viewModel.fileEdit(null);
                    statusbarObj.reset();
                }).fail(function (error) {
                    viewModel.fileEdit(null);
                    showError(error);
                });
        }
    }

    var root = new node({ name: "/", type: "dir", href: appRoot + "api/vfs/" }),
        ignoreWorkingDirChange = false,
        workingDirChanging = false,
        viewModel = {
            root: root,
            specialDirs: ko.observableArray([]),
            selected: ko.observable(root),
            koprocessing: ko.observable(false),
            fileEdit: ko.observable(null),
            editText: ko.observable(""),
            cancelEdit: function () {
                viewModel.fileEdit(null);
                statusbarObj.reset();
            },
            selectSpecialDir: function (name) {
                var item = viewModel.specialDirsIndex()[name];
                if (item) {
                    item.selectNode();
                }
            },
            errorText: ko.observable(),
            inprocessing: 0,
            processing: function (value) {
                value ? viewModel.inprocessing++ : viewModel.inprocessing--;
                viewModel.inprocessing > 0 ? viewModel.koprocessing(true) : viewModel.koprocessing(false);
            }
        };

    viewModel.specialDirsIndex = ko.dependentObservable(function () {
        var result = {};
        ko.utils.arrayForEach(viewModel.specialDirs(), function (value) {
            result[value.name()] = value;
        });
        return result;
    }, viewModel),

    viewModel.koprocessing.subscribe(function (newValue) {
        if (newValue) {
            viewModel.errorText("");
        }
    });

    viewModel.showSiteRoot = ko.computed(function () {
        if ($.isEmptyObject(viewModel.specialDirsIndex())) {
            return true;
        }
        return viewModel.specialDirsIndex()['LocalSiteRoot'] !== undefined;
    }, viewModel);

    root.fetchChildren();
    ko.applyBindings(viewModel, document.getElementById("#main"));
    setupFileSystemWatcher();

    window.KuduExec.workingDir.subscribe(function (newValue) {
        if (ignoreWorkingDirChange) {
            ignoreWorkingDirChange = false;
            return;
        }

        function getRelativePath(parent, childDir) {
            var parentPath = (parent.path() || window.KuduExec.appRoot).toLowerCase();
            if (childDir.length >= parentPath.length && childDir.toLowerCase().indexOf(parentPath) === 0) {
                return { parent: parent, relativePath: childDir.substring(parentPath.length).replace(/^(\/|\\)?(.*)(\/|\\)?$/g, "$2") };
            }
        }

        workingDirChanging = true;
        var relativeDir = getRelativePath(viewModel.root, newValue) ||
            getRelativePath(viewModel.specialDirsIndex()["LocalSiteRoot"], newValue) ||
            getRelativePath(viewModel.specialDirsIndex()["SystemDrive"], newValue),
            deferred;

        if (!relativeDir || !relativeDir.relativePath) {
            deferred = ((relativeDir && relativeDir.parent) || viewModel.root).selectNode();
        } else {
            stashCurrentSelection(viewModel.selected());
            deferred = relativeDir.parent.selectChild(relativeDir.relativePath);
        }
        deferred.done(function () {
            workingDirChanging = false;
        });
    });

    viewModel.selected.subscribe(function (newValue) {
        if (!workingDirChanging) {
            // Mark it so that no-op the subscribe callback.
            ignoreWorkingDirChange = true;
            updateFileSystemWatcher(newValue.path());
            window.KuduExec.changeDir(newValue.path());

            newValue.fetchChildren(/* force */ true);
        }
    });

    window.KuduExec.completePath = function (value, dirOnly) {
        var subDirs = value.toLowerCase().split(/\/|\\/),
            cur = viewModel.selected(),
            curToken = "";

        while (subDirs.length && cur) {
            curToken = subDirs.shift();
            if (curToken === ".." && cur && cur.parent) {
                cur = cur.parent;
                continue;
            }

            if (!cur.children || !cur.children().length) {
                cur = null;
                break;
            }

            cur = $.grep(cur.children(), function (elm) {
                if (dirOnly && !elm.isDirectory()) {
                    return false;
                }

                return subDirs.length ? (elm.name().toLowerCase() === curToken) : elm.name().toLowerCase().indexOf(curToken) === 0;
            });

            if (cur && cur.length === 1 && subDirs.length) {
                // If there's more path to traverse and we have exactly one match, return
                cur = cur[0];
            }
        }
        if (cur) {
            return $.map(cur, function (elm) { return elm.name(); });
        }
    };

    //monitor file upload progress 
    function copyProgressHandlingFunction(e,uniqueUrl) {
        if (e.lengthComputable) {
            copyObjectsManager.addCopyStats(uniqueUrl, e.loaded, e.total);
            var perc = copyObjectsManager.getCurrentPercentCompletion();
            $('#copy-percentage').text(perc + "%");
        }
    }

    function setupFileSystemWatcher() {
        updateFileSystemWatcher(null);
    }

    function updateFileSystemWatcher(newValue) {
        window.viewModel = viewModel;
        fileSystemHub.server.register(newValue);
    }

    window.KuduExec.updateFileSystemWatcher = updateFileSystemWatcher;

    function stashCurrentSelection(selected) {
        if (window.history && window.history.pushState) {
            window.history.pushState(selected.path(), selected.name());
        }
    }

    function getLocalStorageSetting(name, defaultValue) {
        try {
            var value = window.localStorage[name];
            if (value === undefined) {
                return defaultValue;
            }

            if (typeof (defaultValue) === "number") {
                return parseInt(value);
            } else if (typeof (defaultValue) === "boolean") {
                return !!value;
            } else {
                return value;
            }
        } catch (e) {
            return defaultValue;
        }
    }

    window.onpopstate = function (evt) {
        if (viewModel.fileEdit()) {
            // If we're editing, exit the editing.
            viewModel.fileEdit(null);
        } else {
            var selected = viewModel.selected();
            if (selected.parent) {
                viewModel.selected(selected.parent);
            }
        }
    };

    $("#fileList").on("keydown", "input[type=text]", function (evt) {
        var context = ko.contextFor(this),
            data = context.$data;

        if (evt.which === 27) { // Cancel if Esc is pressed.
            data.parent.children.remove(data);
            return false;
        }
    });

    $("#createFolder").click(function (evt) {
        evt.preventDefault();

        var newFolder = new node({ name: "", type: "dir", href: "", editing: true }, viewModel.selected());
        $(this).prop("disabled", true);
        viewModel.selected().children.unshift(newFolder);
        $("#fileList input[type='text']").focus();

        newFolder.name.subscribe(function (value) {
            newFolder.href = trimTrailingSlash(newFolder.parent.href) + "/" + value + "/";
            newFolder._href(newFolder.href);
            newFolder.editing(false);
            Vfs.createFolder(newFolder).fail(function () {
                viewModel.selected().children.remove(newFolder);
            });
            $("#createFolder").prop("disabled", false);
        });
    });

    // Drag and drop
    $("#fileList")
        .on("dragenter dragover", function (e) {
            e.preventDefault();
            e.stopPropagation();
            if (_isZipFile(e)) {
                $(".show-on-hover").addClass('upload-unzip-show');
            }
        })
        .on("drop", function (evt) {
            evt.preventDefault();
            evt.stopPropagation();

            $(".show-on-hover").removeClass('upload-unzip-show');
            $(".show-on-hover").removeClass('upload-unzip-hover');
            $("#copy-percentage").text("");
            var dir = viewModel.selected();
            viewModel.processing(true);
            _getInputFiles(evt).done(function (files) {
                Vfs.addFiles(files).always(function () {
                    dir.fetchChildren( /* force */ true);
                    viewModel.processing(false);
                    $("#copy-percentage").text("");
                });
            });
        }).on("dragleave", function (e) {
            $(".show-on-hover").removeClass('upload-unzip-show');
        });

    $("#upload-unzip")
        .on("dragenter dragover", function(e) {
            $(".show-on-hover").addClass('upload-unzip-hover');
        })
        .on("drop", function(evt) {
        evt.preventDefault();
        evt.stopPropagation();

        $(".show-on-hover").removeClass('upload-unzip-show');
        $(".show-on-hover").removeClass('upload-unzip-hover');
        var dir = viewModel.selected();
        viewModel.processing(true);
        _getInputFiles(evt).done(function(files) {
            Vfs.addFiles(files, _isZipFile(evt)).always(function() {
                dir.fetchChildren( /* force */ true);
                viewModel.processing(false);
            });
        });
        }).on("dragleave", function (e) {
            $(".show-on-hover").removeClass('upload-unzip-hover');
        });

    var defaults = { fileList: '40%', console: '45%' };
    $('#resizeHandle .down')
        .on('click', function (e) {
            var fileList = $('#fileList'),
                console = window.$KuduExecConsole;
            if (!console.is(':visible')) {
                return;
            } else if (fileList.is(":visible")) {
                console.slideDown(function () {
                    console.hide();
                    fileList.css('height', '85%');
                });
            } else {
                console.css('height', defaults.console);
                fileList.css('height', defaults.fileList);
                fileList.show();
            }
        });

    $('#resizeHandle .up')
        .on('click', function (e) {
            var fileList = $('#fileList'),
                console = window.$KuduExecConsole;
            if (!fileList.is(':visible')) {
                return;
            } else if (console.is(':visible')) {
                fileList.slideUp(function () {
                    fileList.hide();
                    console.css('height', '85%');
                });
            } else {
                fileList.css('height', defaults.fileList);
                console.css('height', defaults.console);
                console.show();
            }
        });


    function _getInputFiles(evt) {
        var dt = evt.originalEvent.dataTransfer,
            items = evt.originalEvent.dataTransfer.items;

        if (items && items.length) {
            return whenArray($.map(items, function (item) {
                if (item.kind === 'file') {
                    var entry = (item.webkitGetAsEntry || item.getAsEntry).apply(item);
                    return _processEntry(entry);
                }
            })).pipe(function () {
                return Array.prototype.concat.apply([], arguments);
            })
        } else {
            return $.Deferred().resolveWith(null, [$.map(dt.files, function (e) {
                return { name: e.name, contents: e };
            })]);
        }
    }

    function _isZipFile(evt) {
        var items = evt.originalEvent.dataTransfer.items || evt.originalEvent.dataTransfer.files;
        if (items) {
            var filesArray = $.map(items, function(item) {
                if (item.type === 'application/x-zip-compressed' || item.type === 'application/zip' || item.type === '')
                    return item;
            });
            if (filesArray && filesArray.length === items.length) {
                return true;
            } else {
                return false;
            }
        } else {
            //if both items and files are undefined, that means the browser (IE, FF)
            //doesn't support showing files on dragging, only on dropping, then assume a zip file.
            //Extracting will no-op if it's not a zip file anyway.
            return true;
        }
    }

    function _processEntry(entry, parentPath) {
        parentPath = parentPath || '';
        var deferred = $.Deferred();
        if (entry.isFile) {
            entry.file(function (file) {
                deferred.resolveWith(null, [{ name: parentPath + '/' + entry.name, contents: file }]);
            });
        } else {
            entry.createReader().readEntries(function (entries) {
                var directoryPath = parentPath + '/' + entry.name;
                whenArray($.map(entries, function (e) {
                    return _processEntry(e, directoryPath);
                })).done(function () {
                    deferred.resolveWith(null, [Array.prototype.concat.apply([], arguments)]);
                });;
            });
        }
        return deferred.promise();
    }

    function whenArray(deferreds) {
        return $.when.apply($, deferreds);
    }

    function trimTrailingSlash(input) {
        return input.replace(/(\/|\\)$/, '');
    }

    function showError(error) {
        if (error.status === 403) {
            $('#403-error-modal').modal();
        }
        viewModel.processing(false);
        viewModel.errorText(JSON.parse(error.responseText).Message);
    }
});
