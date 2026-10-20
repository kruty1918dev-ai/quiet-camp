package com.quietcamp.game.privacy;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.net.Uri;

/** Runs before FirebaseInitProvider (order 100). Clear the persisted Analytics
 * collection override before native Firebase startup; C# restores current consent.
 * Reflection keeps this small Android library independent of SDK compile deps.
 */
public final class FirebasePrivacyProvider extends ContentProvider {
    @Override public boolean onCreate() {
        try {
            Class<?> analytics = Class.forName("com.google.firebase.analytics.FirebaseAnalytics");
            Object instance = analytics.getMethod("getInstance", android.content.Context.class).invoke(null, getContext());
            analytics.getMethod("setAnalyticsCollectionEnabled", boolean.class).invoke(instance, false);
        } catch (Exception failure) {
            android.util.Log.w("QuietCampPrivacy", "Analytics startup gate unavailable: " + failure.getClass().getSimpleName());
        }
        return true;
    }
    @Override public Cursor query(Uri uri, String[] projection, String selection, String[] args, String order) { return null; }
    @Override public String getType(Uri uri) { return null; }
    @Override public Uri insert(Uri uri, ContentValues values) { return null; }
    @Override public int delete(Uri uri, String selection, String[] args) { return 0; }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] args) { return 0; }
}
