package com.quietcamp.game.privacy;

import android.app.Activity;
import android.app.Application;
import android.os.Bundle;
import android.os.CancellationSignal;
import android.os.Handler;
import android.os.Looper;
import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import java.security.SecureRandom;
import java.util.concurrent.Executor;
import android.util.Base64;

/** Explicit Google button flow. No browser, fake chooser, analytics or token logging. */
public final class GoogleCredentialBridge {
    public interface Callback { void success(String value); void failure(String code); }
    private static final Handler MAIN = new Handler(Looper.getMainLooper());
    private static final Executor EXECUTOR = command -> MAIN.post(command);
    private GoogleCredentialBridge() { }

    public static Request choose(Activity activity, String serverClientId, Callback callback) {
        Request request = new Request(activity, callback);
        MAIN.post(() -> request.start(serverClientId));
        return request;
    }
    public static void clear(Activity activity, Callback callback) {
        MAIN.post(() -> {
            try {
                CredentialManager.create(activity.getApplicationContext()).clearCredentialStateAsync(
                    new ClearCredentialStateRequest(), null, EXECUTOR,
                    new CredentialManagerCallback<Void, ClearCredentialException>() {
                        @Override public void onResult(Void ignored) { callback.success(""); }
                        @Override public void onError(ClearCredentialException error) { callback.failure("unavailable"); }
                    });
            } catch (Exception error) { callback.failure("unavailable"); }
        });
    }
    public static final class Request implements Application.ActivityLifecycleCallbacks {
        private Activity activity;
        private Callback callback;
        private final CancellationSignal signal = new CancellationSignal();
        private boolean registered;
        Request(Activity activity, Callback callback) { this.activity=activity; this.callback=callback; }
        void start(String serverClientId) {
            if (callback==null) return;
            if (activity.isFinishing() || activity.isDestroyed()) { fail("cancelled"); return; }
            try {
                activity.getApplication().registerActivityLifecycleCallbacks(this); registered=true;
                byte[] bytes=new byte[32]; new SecureRandom().nextBytes(bytes);
                String nonce=Base64.encodeToString(bytes, Base64.URL_SAFE | Base64.NO_WRAP | Base64.NO_PADDING);
                GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(serverClientId).setNonce(nonce).build();
                GetCredentialRequest request=new GetCredentialRequest.Builder().addCredentialOption(option).build();
                CredentialManager.create(activity).getCredentialAsync(activity, request, signal, EXECUTOR,
                    new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                        @Override public void onResult(GetCredentialResponse result) {
                            if (callback==null) return;
                            try {
                                if (!(result.getCredential() instanceof CustomCredential)) { fail("unavailable"); return; }
                                CustomCredential credential=(CustomCredential)result.getCredential();
                                if (!GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) { fail("unavailable"); return; }
                                String token=GoogleIdTokenCredential.createFrom(credential.getData()).getIdToken();
                                Callback target=callback; finish(); target.success(token);
                            } catch (Exception error) { fail("unavailable"); }
                        }
                        @Override public void onError(GetCredentialException error) {
                            fail(error instanceof GetCredentialCancellationException ? "cancelled"
                                : error instanceof NoCredentialException ? "no_credentials" : "unavailable");
                        }
                    });
            } catch (Exception error) { fail("unavailable"); }
        }
        public void cancel() { MAIN.post(() -> { signal.cancel(); fail("cancelled"); }); }
        private void fail(String code) { Callback target=callback; finish(); if(target!=null) target.failure(code); }
        private void finish() {
            if (registered) { activity.getApplication().unregisterActivityLifecycleCallbacks(this); registered=false; }
            callback=null; activity=null;
        }
        @Override public void onActivityDestroyed(Activity value) { if(value==activity) cancel(); }
        @Override public void onActivityCreated(Activity a, Bundle state) { }
        @Override public void onActivityStarted(Activity a) { }
        @Override public void onActivityResumed(Activity a) { }
        @Override public void onActivityPaused(Activity a) { }
        @Override public void onActivityStopped(Activity a) { }
        @Override public void onActivitySaveInstanceState(Activity a, Bundle state) { }
    }
}
