package com.quietcamp.game.billing;

import android.app.Activity;
import android.content.SharedPreferences;
import com.android.billingclient.api.*;
import org.json.JSONArray;
import org.json.JSONObject;
import java.util.*;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

/** Polling JNI boundary. All store work is on the UI thread; no Unity callback names. */
@androidx.annotation.Keep
public final class PlayBillingBridge implements PurchasesUpdatedListener {
    private final Activity activity;
    private final SharedPreferences history;
    private final AtomicInteger sequence = new AtomicInteger();
    private final Map<Integer, String> results = new ConcurrentHashMap<>();
    private final Set<Integer> active = ConcurrentHashMap.newKeySet();
    private final List<Runnable> connected = new ArrayList<>();
    private BillingClient client;
    private boolean connecting, closed;
    private int purchaseRequest;
    private String purchaseProduct;
    public PlayBillingBridge(Activity activity) {
        this.activity = activity;
        history = activity.getSharedPreferences("qc_play_tokens", 0);
    }
    public int request(String operation, String product) {
        int id = sequence.incrementAndGet(); active.add(id);
        activity.runOnUiThread(() -> connect(() -> {
            if ("query".equals(operation)) query(id, product);
            else details(id, product, "buy".equals(operation));
        }));
        return id;
    }
    public String poll(int id) { return results.remove(id); }
    public void abandon(int id) { active.remove(id); results.remove(id); }
    public static boolean clearHistory(Activity activity) { return activity.getSharedPreferences("qc_play_tokens", 0).edit().clear().commit(); }
    private void finish(int id, JSONObject response) {
        if (active.remove(id)) results.put(id, response.toString());
    }
    private JSONObject response(String state) {
        JSONObject value = new JSONObject();
        try { value.put("state", state); } catch (Exception ignored) { }
        return value;
    }
    private void connect(Runnable work) {
        if (closed) { failAll(); return; }
        if (client == null) client = BillingClient.newBuilder(activity).setListener(this)
            .enablePendingPurchases(PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())
            .enableAutoServiceReconnection().build();
        if (client.isReady()) { work.run(); return; }
        connected.add(work);
        if (connecting) return;
        connecting = true;
        client.startConnection(new BillingClientStateListener() {
            public void onBillingSetupFinished(BillingResult result) {
                connecting = false;
                List<Runnable> queue = new ArrayList<>(connected); connected.clear();
                if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) { failAll(); return; }
                for (Runnable callback : queue) callback.run();
            }
            public void onBillingServiceDisconnected() { connecting = false; }
        });
    }
    private void failAll() {
        connected.clear(); purchaseRequest = 0;
        for (int id : new HashSet<>(active)) finish(id, response("Unavailable"));
    }
    private void details(int id, String product, boolean buy) {
        QueryProductDetailsParams.Product item = QueryProductDetailsParams.Product.newBuilder()
            .setProductId(product).setProductType(BillingClient.ProductType.INAPP).build();
        client.queryProductDetailsAsync(QueryProductDetailsParams.newBuilder().setProductList(Collections.singletonList(item)).build(), (result, detailsResult) -> {
            if (!active.contains(id) || closed) return;
            if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) { finish(id, response("Unavailable")); return; }
            ProductDetails details = null;
            for (ProductDetails candidate : detailsResult.getProductDetailsList())
                if (product.equals(candidate.getProductId())) details = candidate;
            if (details == null || details.getOneTimePurchaseOfferDetails() == null) { finish(id, response("Unavailable")); return; }
            if (!buy) {
                JSONObject value = response("Available");
                try { value.put("productId", product); value.put("price", details.getOneTimePurchaseOfferDetails().getFormattedPrice()); }
                catch (Exception ignored) { }
                finish(id, value); return;
            }
            if (purchaseRequest != 0) { finish(id, response("Busy")); return; }
            purchaseRequest = id; purchaseProduct = product;
            BillingFlowParams.ProductDetailsParams itemParams = BillingFlowParams.ProductDetailsParams.newBuilder().setProductDetails(details).setOfferToken(details.getOneTimePurchaseOfferDetails().getOfferToken()).build();
            BillingResult launch = client.launchBillingFlow(activity, BillingFlowParams.newBuilder().setProductDetailsParamsList(Collections.singletonList(itemParams)).build());
            if (launch.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                purchaseRequest = 0;
                if (launch.getResponseCode() == BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED) query(id, product);
                else finish(id, response(launch.getResponseCode() == BillingClient.BillingResponseCode.USER_CANCELED ? "Cancelled" : "Unavailable"));
            }
        });
    }
    private JSONObject token(String product, String token) throws Exception {
        JSONObject result = new JSONObject(); result.put("productId", product); result.put("purchaseToken", token); return result;
    }
    private void query(int id, String product) {
        client.queryPurchasesAsync(QueryPurchasesParams.newBuilder().setProductType(BillingClient.ProductType.INAPP).build(), (result, purchases) -> {
            if (result.getResponseCode() != BillingClient.BillingResponseCode.OK) { finish(id, response("Unavailable")); return; }
            try {
                Set<String> tokens = new HashSet<>(history.getStringSet(product, Collections.emptySet()));
                for (Purchase purchase : purchases) if (purchase.getProducts().contains(product)) tokens.add(purchase.getPurchaseToken());
                if (!history.edit().putStringSet(product, tokens).commit()) { finish(id, response("Unavailable")); return; }
                JSONArray list = new JSONArray(); for (String purchaseToken : tokens) list.put(token(product, purchaseToken));
                JSONObject value = response("Available"); value.put("purchases", list); finish(id, value);
            } catch (Exception ignored) { finish(id, response("Unavailable")); }
        });
    }
    @Override public void onPurchasesUpdated(BillingResult result, List<Purchase> purchases) {
        int id = purchaseRequest;
        if (result.getResponseCode() == BillingClient.BillingResponseCode.OK && purchases != null) {
            try {
                JSONArray list = new JSONArray();
                for (Purchase purchase : purchases) for (String product : purchase.getProducts()) {
                    Set<String> tokens = new HashSet<>(history.getStringSet(product, Collections.emptySet()));
                    tokens.add(purchase.getPurchaseToken());
                    if (!history.edit().putStringSet(product, tokens).commit()) throw new IllegalStateException();
                    if (id != 0 && product.equals(purchaseProduct)) list.put(token(product, purchase.getPurchaseToken()));
                }
                if (id != 0) { JSONObject value = response("Available"); value.put("purchases", list); finish(id, value); }
            } catch (Exception ignored) { if (id != 0) finish(id, response("Unavailable")); }
        } else if (id != 0) finish(id, response(result.getResponseCode() == BillingClient.BillingResponseCode.USER_CANCELED ? "Cancelled" : "Unavailable"));
        purchaseRequest = 0;
    }
    public void close() { closed = true; activity.runOnUiThread(() -> { failAll(); if (client != null) client.endConnection(); }); }
}
