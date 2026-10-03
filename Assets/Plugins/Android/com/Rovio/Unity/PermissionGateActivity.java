package com.Rovio.Unity;

import android.app.Activity;
import android.Manifest;
import android.content.pm.PackageManager;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.Settings;
import android.util.Log;

import com.unity3d.player.UnityPlayerActivity;

public class PermissionGateActivity extends Activity {
    private static final int STORAGE_REQUEST = 0x2BAE;
    private static final String PROMPT_ACCEPTED = "storage_prompt_accepted";
    private boolean m_waitingForSettings;
    private boolean m_settingsPaused;
    private boolean m_gameLaunched;
    private AlertDialog m_permissionDialog;
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (savedInstanceState != null) {
            m_waitingForSettings = savedInstanceState.getBoolean("waitingForSettings");
            m_settingsPaused = m_waitingForSettings;
        }
        if (HasAllFilesAccess() || (!m_waitingForSettings &&
            getPreferences(MODE_PRIVATE).getBoolean(PROMPT_ACCEPTED, false))) {
            LaunchGame();
            return;
        }
        if (!m_waitingForSettings) ShowInitialPermissionDialog();
    }
    @Override
    protected void onResume() {
        super.onResume();
        if (HasAllFilesAccess() || (m_waitingForSettings && m_settingsPaused)) {
            m_waitingForSettings = false;
            LaunchGame();
        }
    }
    @Override
    protected void onPause() {
        super.onPause();
        if (m_waitingForSettings) m_settingsPaused = true;
    }
    @Override
    protected void onSaveInstanceState(Bundle state) {
        state.putBoolean("waitingForSettings", m_waitingForSettings);
        super.onSaveInstanceState(state);
    }
    private boolean HasAllFilesAccess() {
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                return Environment.isExternalStorageManager();
            }
            if (Build.VERSION.SDK_INT >= 23) {
                return checkSelfPermission(Manifest.permission.WRITE_EXTERNAL_STORAGE) == PackageManager.PERMISSION_GRANTED;
            }
            return true;
        } catch (RuntimeException exception) {
            Log.w("INStorage", "Cannot query public storage permission", exception);
            return false;
        }
    }
    private String GetAllowedFolderText() {
        String text = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS).getAbsolutePath();
        CharSequence charSequence = getApplicationInfo().loadLabel(getPackageManager());
        if (charSequence != null) {
            text = text + "/" + charSequence.toString();
        }
        return text;
    }
    private void ShowInitialPermissionDialog() {
        if (m_permissionDialog != null || m_gameLaunched) {
            return;
        }
        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setTitle("Public save folder access");
        builder.setMessage("Storage access lets the game use existing files at: " + GetAllowedFolderText() +
            ". You can also continue using app storage. Existing saves are kept.");
        builder.setCancelable(false);
        builder.setPositiveButton("Open settings", new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialogInterface, int i) {
                AcceptStorageChoice();
                RequestPermission();
            }
        });
        builder.setNegativeButton("Continue", new DialogInterface.OnClickListener() {
            @Override
            public void onClick(DialogInterface dialogInterface, int i) {
                AcceptStorageChoice();
                LaunchGame();
            }
        });
        m_permissionDialog = builder.show();
    }
    private void AcceptStorageChoice() {
        getPreferences(MODE_PRIVATE).edit().putBoolean(PROMPT_ACCEPTED, true).apply();
    }
    private void RequestPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            Uri packageUri = Uri.parse("package:" + getPackageName());
            if (OpenPermissionSettings(new Intent(Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION, packageUri))) return;
            if (OpenPermissionSettings(new Intent(Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION))) return;
            if (OpenPermissionSettings(new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, packageUri))) return;
            LaunchGame();
            return;
        }
        if (Build.VERSION.SDK_INT >= 23) {
            try {
                requestPermissions(new String[] { Manifest.permission.READ_EXTERNAL_STORAGE,
                    Manifest.permission.WRITE_EXTERNAL_STORAGE }, STORAGE_REQUEST);
            } catch (RuntimeException exception) {
                Log.w("INStorage", "Cannot request public storage permission", exception);
                LaunchGame();
            }
        } else {
            LaunchGame();
        }
    }
    private boolean OpenPermissionSettings(Intent intent) {
        try {
            m_waitingForSettings = true;
            m_settingsPaused = false;
            startActivityForResult(intent, STORAGE_REQUEST);
            return true;
        } catch (RuntimeException exception) {
            m_waitingForSettings = false;
            Log.w("INStorage", "Storage settings unavailable: " + intent.getAction(), exception);
            return false;
        }
    }
    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == STORAGE_REQUEST) {
            m_waitingForSettings = false;
            LaunchGame();
        }
    }
    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == STORAGE_REQUEST) LaunchGame();
    }
    private void LaunchGame() {
        if (m_gameLaunched || isFinishing()) return;
        m_gameLaunched = true;
        if (m_permissionDialog != null) {
            m_permissionDialog.dismiss();
            m_permissionDialog = null;
        }
        Intent intent = new Intent(this, UnityPlayerActivity.class);
        startActivity(intent);
        finish();
    }
}
