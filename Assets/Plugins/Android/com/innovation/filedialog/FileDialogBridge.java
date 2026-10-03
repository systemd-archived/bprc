package com.innovation.filedialog;

import android.app.Activity;
import android.content.Intent;
import android.content.ContentResolver;
import android.database.Cursor;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.DocumentsContract;
import android.provider.OpenableColumns;
import android.util.Log;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

// SAF owns access to the selected URI; Unity sees a cache file or an export result label.
public class FileDialogBridge {
    private static final int REQUEST_DOCUMENT = 0x2BAD;
    private static volatile String s_resultPath = "";
    private static volatile String s_error = "";
    private static volatile boolean s_resultReady;
    private static volatile String s_mimeType = "*/*";

    public static void SetMimeType(String mime) {
        s_mimeType = (mime == null || mime.isEmpty()) ? "*/*" : mime;
    }
    public static void OpenFile() { launch("open", "", ""); }
    public static void SaveFile(String source, String name) { launch("save", source, name); }
    public static void OpenFolder(String path) { launch("folder", path, ""); }
    public static boolean IsReady() { return s_resultReady; }
    public static String GetResult() { return s_resultPath; }
    public static String GetError() { return s_error; }

    private static void complete(String result, String error) {
        s_resultPath = result == null ? "" : result;
        s_error = error == null ? "" : error;
        s_resultReady = true;
    }

    private static void launch(final String mode, final String source, final String name) {
        s_resultReady = false;
        s_resultPath = "";
        s_error = "";
        try {
            // UnityPlayer.currentActivity is a public static FIELD, not a method.
            final Activity activity = (Activity) Class.forName("com.unity3d.player.UnityPlayer")
                .getField("currentActivity").get(null);
            if (activity == null) throw new IllegalStateException("Unity activity is unavailable");
            activity.runOnUiThread(new Runnable() {
                @Override public void run() {
                    try {
                        Intent intent = new Intent(activity, FileDialogActivity.class);
                        intent.putExtra("mode", mode);
                        intent.putExtra("source", source);
                        intent.putExtra("name", name);
                        intent.putExtra("mimeType", s_mimeType);
                        activity.startActivity(intent);
                    } catch (Exception e) { complete("", e.toString()); }
                }
            });
        } catch (Exception e) {
            Log.e("INFileDialog", "Cannot start document activity", e);
            complete("", e.toString());
        }
    }

    private static void copy(InputStream input, OutputStream output) throws Exception {
        if (input == null || output == null) throw new java.io.IOException("Document stream is unavailable");
        byte[] buffer = new byte[32768];
        int count;
        while ((count = input.read(buffer)) != -1) output.write(buffer, 0, count);
    }

    private static String importDocument(Uri uri, Activity activity) throws Exception {
        ContentResolver resolver = activity.getContentResolver();
        String name = "selected_file";
        try (Cursor cursor = resolver.query(uri, new String[] { OpenableColumns.DISPLAY_NAME }, null, null, null)) {
            if (cursor != null && cursor.moveToFirst()) {
                String displayName = cursor.getString(0);
                if (displayName != null && !displayName.isEmpty()) name = new File(displayName).getName();
            }
        }
        File directory = File.createTempFile("document_", "", activity.getCacheDir());
        if (!directory.delete() || !directory.mkdir()) throw new java.io.IOException("Cannot create import cache");
        File destination = new File(directory, name);
        try (InputStream input = resolver.openInputStream(uri); OutputStream output = new FileOutputStream(destination)) {
            copy(input, output);
        } catch (Exception e) {
            destination.delete();
            directory.delete();
            throw e;
        }
        return destination.getAbsolutePath();
    }

    private static Uri folderUri(String path) {
        if (path == null) return null;
        String root = Environment.getExternalStorageDirectory().getAbsolutePath() + "/";
        String normalized = path.replace('\\', '/');
        if (normalized.startsWith("/sdcard/")) normalized = root + normalized.substring(8);
        if (!normalized.startsWith(root)) return null;
        String relative = normalized.substring(root.length());
        while (relative.endsWith("/")) relative = relative.substring(0, relative.length() - 1);
        // DocumentsContract encodes the document ID itself. Never pre-encode it.
        return DocumentsContract.buildDocumentUri("com.android.externalstorage.documents", "primary:" + relative);
    }

    public static class FileDialogActivity extends Activity {
        @Override protected void onCreate(Bundle savedInstanceState) {
            super.onCreate(savedInstanceState);
            // A recreated proxy awaits the existing picker instead of opening another.
            if (savedInstanceState != null) return;
            String mode = getIntent().getStringExtra("mode");
            String mime = getIntent().getStringExtra("mimeType");
            if (mime == null || mime.isEmpty()) mime = "*/*";
            try {
                Intent intent;
                if ("folder".equals(mode)) {
                    Uri uri = folderUri(getIntent().getStringExtra("source"));
                    if (uri != null) {
                        try {
                            Intent view = new Intent(Intent.ACTION_VIEW);
                            view.setDataAndType(uri, DocumentsContract.Document.MIME_TYPE_DIR);
                            view.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                            startActivity(view);
                            complete("", "");
                            finish();
                            return;
                        } catch (Exception e) {
                            Log.d("INFileDialog", "Using system directory picker", e);
                        }
                    }
                    intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
                    if (uri != null && Build.VERSION.SDK_INT >= 26) intent.putExtra(DocumentsContract.EXTRA_INITIAL_URI, uri);
                } else {
                    intent = new Intent("save".equals(mode) ? Intent.ACTION_CREATE_DOCUMENT : Intent.ACTION_OPEN_DOCUMENT);
                    intent.addCategory(Intent.CATEGORY_OPENABLE);
                    intent.setType("save".equals(mode) ? "application/octet-stream" : mime);
                    if ("save".equals(mode)) intent.putExtra(Intent.EXTRA_TITLE, getIntent().getStringExtra("name"));
                }
                intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
                startActivityForResult(intent, REQUEST_DOCUMENT);
            } catch (Exception e) {
                complete("", e.toString());
                finish();
            }
        }

        @Override protected void onActivityResult(int requestCode, int resultCode, Intent data) {
            super.onActivityResult(requestCode, resultCode, data);
            if (requestCode != REQUEST_DOCUMENT) return;
            if (resultCode != RESULT_OK || data == null || data.getData() == null || "folder".equals(getIntent().getStringExtra("mode"))) {
                complete("", "");
                finish();
                return;
            }
            final Uri uri = data.getData();
            // Cloud/removable providers can block: copy off the Android UI thread.
            new Thread(new Runnable() {
                @Override public void run() {
                    String result = "";
                    String error = "";
                    try {
                        if ("save".equals(getIntent().getStringExtra("mode"))) {
                            try (InputStream input = new FileInputStream(getIntent().getStringExtra("source"));
                                 OutputStream output = getContentResolver().openOutputStream(uri, "wt")) {
                                copy(input, output);
                            }
                            result = uri.toString();
                        } else result = importDocument(uri, FileDialogActivity.this);
                    } catch (Exception e) {
                        Log.e("INFileDialog", "Document copy failed", e);
                        error = e.toString();
                    }
                    complete(result, error);
                    runOnUiThread(new Runnable() { @Override public void run() { finish(); } });
                }
            }, "INDocumentCopy").start();
        }
    }
}
