package com.quietcamp.game.privacy;

import android.content.Context;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.AtomicFile;
import java.io.File;
import java.io.FileOutputStream;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** Device-bound cloud session, excluded from backups. No logging and no exported component. */
public final class CloudCredentialStorage {
    private static final String ALIAS="quietcamp.cloud.session.v1";
    private static AtomicFile file(Context context) { return new AtomicFile(new File(context.getNoBackupFilesDir(),"qc-cloud-session")); }
    private static SecretKey key() throws Exception {
        KeyStore store=KeyStore.getInstance("AndroidKeyStore"); store.load(null);
        if (!store.containsAlias(ALIAS)) {
            KeyGenerator generator=KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,"AndroidKeyStore");
            generator.init(new KeyGenParameterSpec.Builder(ALIAS,KeyProperties.PURPOSE_ENCRYPT|KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true).build());
            generator.generateKey();
        }
        return (SecretKey)store.getKey(ALIAS,null);
    }
    public static synchronized String read(Context context) {
        try {
            AtomicFile storage=file(context);
            if (!storage.getBaseFile().exists()) return "";
            byte[] bytes=storage.readFully();
            if (bytes.length<29 || bytes.length>16384) return "";
            byte[] iv=new byte[12]; System.arraycopy(bytes,0,iv,0,12);
            Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE,key(),new GCMParameterSpec(128,iv));
            return new String(cipher.doFinal(bytes,12,bytes.length-12),StandardCharsets.UTF_8);
        } catch(Exception ignored) { return ""; }
    }
    public static synchronized boolean write(Context context,String json) {
        AtomicFile storage=file(context); FileOutputStream output=null;
        try {
            if (json==null || json.isEmpty()) {
                storage.delete();
                KeyStore store=KeyStore.getInstance("AndroidKeyStore"); store.load(null); store.deleteEntry(ALIAS);
                return !storage.getBaseFile().exists();
            }
            byte[] clear=json.getBytes(StandardCharsets.UTF_8);
            if (clear.length>8192) return false;
            Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding"); cipher.init(Cipher.ENCRYPT_MODE,key());
            byte[] encrypted=cipher.doFinal(clear),iv=cipher.getIV();
            if (iv.length!=12) return false;
            output=storage.startWrite(); output.write(iv); output.write(encrypted); storage.finishWrite(output);
            java.util.Arrays.fill(clear,(byte)0); return true;
        } catch(Exception ignored) { if(output!=null)storage.failWrite(output); return false; }
    }
    private CloudCredentialStorage() {}
}
