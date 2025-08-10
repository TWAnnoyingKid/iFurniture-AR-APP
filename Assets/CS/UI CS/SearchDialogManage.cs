using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;

public class SearchDialogManager : MonoBehaviour
{
    [Header("搜尋對話框 UI 元件")]
    public GameObject searchDialog;
    public Dropdown categoryDropdown;         
    public Button confirmButton;
    public Button cancelButton;
    public Button searchButton;
    public Button clearSearchButton;
    public Button reloadButton;             

    [Header("載入和狀態 UI")]
    public GameObject searchLoadingPanel;
    public Text listTitle;                  
    public GameObject noResultsPanel;
    public GameObject errorPanel;
    public Text errorMessageText;        

    [Header("API 設定")]
    public string apiBaseUrl = "http://140.127.114.38:5008/php";

    private Dictionary<string, string> categoryMapping = new Dictionary<string, string>()
    {
        { "椅子", "chair" },
        { "桌子", "desk" },
        { "沙發", "sofa" },
        { "床具", "bed" },
        { "櫃子", "cabinet" },
        { "裝飾品", "decoration" },
        { "燈具", "lamp" },
        { "收納用品", "storage" },
        { "其他", "other" }
    };

    private ProductListManager productListManager;
    private List<ProductData> originalProducts = new List<ProductData>();
    private bool isInSearchMode = false;
    private string currentCategoryKey = "";
    private string currentCategoryDisplay = "";

    void Start()
    {
        productListManager = FindObjectOfType<ProductListManager>();
        if (productListManager == null)
        {
            Debug.LogError("未找到 ProductListManager！");
        }

        InitializeUI();
        SetupButtonEvents();
        SetupCategoryDropdown();
        
        if (searchDialog != null)
        {
            searchDialog.SetActive(false);
        }
    }

    private void InitializeUI()
    {
        if (searchLoadingPanel != null)
            searchLoadingPanel.SetActive(false);
        
        if (noResultsPanel != null)
            noResultsPanel.SetActive(false);
        
        if (errorPanel != null)
            errorPanel.SetActive(false);
        
        if (listTitle != null)
            listTitle.text = "所有商品";
    }

    private void SetupButtonEvents()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmSearch);
        
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelSearch);
        
        if (searchButton != null)
            searchButton.onClick.AddListener(OpenSearchDialog);
        
        if (clearSearchButton != null)
            clearSearchButton.onClick.AddListener(ClearSearchResults);
        
        if (reloadButton != null)
            reloadButton.onClick.AddListener(ReloadCurrentPage);
    }

    private void SetupCategoryDropdown()
    {
        if (categoryDropdown == null) return;

        categoryDropdown.ClearOptions();
        List<string> categoryOptions = new List<string>();
        foreach (var category in categoryMapping.Keys)
        {
            categoryOptions.Add(category);
        }
        categoryDropdown.AddOptions(categoryOptions);
        categoryDropdown.value = 0;
    }

    public void OpenSearchDialog()
    {
        if (searchDialog != null)
        {
            searchDialog.SetActive(true);
            Debug.Log("開啟搜尋對話框");
        }
    }

    public void OnCancelSearch()
    {
        if (searchDialog != null)
        {
            searchDialog.SetActive(false);
            Debug.Log("取消搜尋");
        }
    }

    public void OnConfirmSearch()
    {
        if (categoryDropdown == null) return;

        string selectedCategoryDisplay = categoryDropdown.options[categoryDropdown.value].text;
        if (!categoryMapping.TryGetValue(selectedCategoryDisplay, out string categoryKey))
        {
            Debug.LogError($"無效的類別選擇: {selectedCategoryDisplay}");
            return;
        }

        Debug.Log($"開始搜尋類別: {selectedCategoryDisplay} ({categoryKey})");

        // 記錄當前搜尋的類別
        currentCategoryKey = categoryKey;
        currentCategoryDisplay = selectedCategoryDisplay;

        if (searchDialog != null)
        {
            searchDialog.SetActive(false);
        }

        StartCoroutine(PerformSearch(categoryKey, selectedCategoryDisplay));
    }

    public void ClearSearchResults()
    {
        Debug.Log("清除搜尋結果，恢復原始商品列表");
        
        // 清除當前搜尋狀態
        currentCategoryKey = "";
        currentCategoryDisplay = "";
        
        if (listTitle != null)
        {
            listTitle.text = "所有商品";
        }

        HideStatusPanels();

        if (isInSearchMode)
        {
            isInSearchMode = false;
            RestoreOriginalProducts();
        }
        else
        {
            StartCoroutine(ReloadAllProducts());
        }
    }

    public void ReloadCurrentPage()
    {
        Debug.Log("重新載入當前頁面");
        
        // 隱藏所有狀態面板
        HideStatusPanels();
        
        // 關閉搜尋對話框（如果開啟）
        if (searchDialog != null && searchDialog.activeInHierarchy)
        {
            searchDialog.SetActive(false);
        }
        
        // 顯示載入面板
        ShowLoadingPanel(true);
        
        // 清空並重新載入
        if (productListManager != null)
        {
            productListManager.allProducts.Clear();
            ClearProductUI();
            
            // 根據當前狀態決定重新載入的內容
            if (isInSearchMode && !string.IsNullOrEmpty(currentCategoryKey))
            {
                // 如果在搜尋模式，重新執行相同的搜尋
                Debug.Log($"重新載入搜尋類別: {currentCategoryDisplay}");
                StartCoroutine(ReloadSearchCategory());
            }
            else
            {
                // 如果在總商品頁面，重新載入所有商品
                Debug.Log("重新載入所有商品");
                // 重置搜尋狀態
                isInSearchMode = false;
                currentCategoryKey = "";
                currentCategoryDisplay = "";
                originalProducts.Clear();
                
                if (listTitle != null)
                {
                    listTitle.text = "所有商品";
                }
                
                StartCoroutine(ReloadProductsAndHideLoading());
            }
        }
    }
    
    private IEnumerator ReloadSearchCategory()
    {
        // 重新執行當前類別的搜尋
        yield return StartCoroutine(PerformSearch(currentCategoryKey, currentCategoryDisplay));
    }
    
    private IEnumerator ReloadProductsAndHideLoading()
    {
        yield return StartCoroutine(productListManager.DownloadProductJson());
        ShowLoadingPanel(false);
    }

    private IEnumerator PerformSearch(string categoryKey, string categoryDisplay)
    {
        ShowLoadingPanel(true);
        HideStatusPanels();

        // 備份原始商品
        if (!isInSearchMode)
        {
            BackupOriginalProducts();
        }

        // 第一步：獲取商品ID
        string searchIdUrl = $"{apiBaseUrl}/searchID.php?category={categoryKey}";
        UnityWebRequest idRequest = UnityWebRequest.Get(searchIdUrl);
        yield return idRequest.SendWebRequest();

        if (idRequest.result != UnityWebRequest.Result.Success)
        {
            ShowErrorMessage($"搜尋失敗: {idRequest.error}");
            ShowLoadingPanel(false);
            yield break;
        }

        List<string> productIds = ParseProductIds(idRequest.downloadHandler.text);
        
        if (productIds.Count == 0)
        {
            // 更新標題顯示搜尋類別但沒有結果
            if (listTitle != null)
            {
                listTitle.text = $"搜尋結果: {categoryDisplay} (0個)";
            }
            
            isInSearchMode = true;
            ShowNoResultsMessage($"未找到 {categoryDisplay} 類別的商品");
            ShowLoadingPanel(false);
            yield break;
        }

        Debug.Log($"找到 {productIds.Count} 個 {categoryDisplay} 商品 ID");

        // 第二步：獲取商品詳細資料
        string idsParam = "['";
        idsParam += string.Join("','", productIds);
        idsParam += "']";
        
        string encodedIds = UnityWebRequest.EscapeURL(idsParam);
        string searchUrl = $"{apiBaseUrl}/products.php?action=get_search&ids={encodedIds}";
        
        Debug.Log($"搜尋商品 URL: {searchUrl}");
        
        UnityWebRequest productsRequest = UnityWebRequest.Get(searchUrl);
        yield return productsRequest.SendWebRequest();

        if (productsRequest.result != UnityWebRequest.Result.Success)
        {
            ShowErrorMessage($"獲取商品資料失敗: {productsRequest.error}");
            ShowLoadingPanel(false);
            yield break;
        }

        // 第三步：處理搜尋結果
        string productsJson = productsRequest.downloadHandler.text;
        Debug.Log($"收到商品資料: {productsJson.Substring(0, Mathf.Min(200, productsJson.Length))}...");
        
        yield return StartCoroutine(ProcessAndDisplayResults(productsJson, categoryDisplay));
        
        ShowLoadingPanel(false);
    }

    private IEnumerator ProcessAndDisplayResults(string jsonText, string categoryDisplay)
    {
        APIResponse response = JsonHelper.FromJson(jsonText);
        
        if (!response.success || response.products.Length == 0)
        {
            // 更新標題顯示搜尋類別但沒有結果
            if (listTitle != null)
            {
                listTitle.text = $"搜尋結果: {categoryDisplay} (0個)";
            }
            
            isInSearchMode = true;
            ShowNoResultsMessage($"未找到 {categoryDisplay} 類別的商品");
            yield break;
        }

        // 更新標題
        if (listTitle != null)
        {
            listTitle.text = $"搜尋結果: {categoryDisplay} ({response.products.Length}個)";
        }

        isInSearchMode = true;
        HideStatusPanels();

        // 清空現有商品列表
        if (productListManager != null)
        {
            productListManager.allProducts.Clear();
            ClearProductUI();

            // 處理每個搜尋到的商品
            foreach (var jp in response.products)
            {
                ProductData pd = new ProductData();
                pd.productName = jp.name;
                pd.price = jp.price;
                pd.url = jp.url;
                pd.otherInfo = jp.description;
                pd.productId = jp.product_id;
                
                // 組合尺寸資訊 - 只顯示有資料的部分
                List<string> sizeComponents = new List<string>();
                if (!string.IsNullOrEmpty(jp.depth) && jp.depth != "0")
                    sizeComponents.Add($"深 {jp.depth}");
                if (!string.IsNullOrEmpty(jp.width) && jp.width != "0")
                    sizeComponents.Add($"寬 {jp.width}");
                if (!string.IsNullOrEmpty(jp.height) && jp.height != "0")
                    sizeComponents.Add($"高 {jp.height}");
                
                pd.sizeOptions = sizeComponents.Count > 0 ? string.Join(" x ", sizeComponents) + " 公分" : "尺寸資訊不完整";
                
                if (jp.model_file_id != null && !string.IsNullOrEmpty(jp.model_file_id))
                {
                    pd.modelURL = $"{apiBaseUrl}/gridfs_file.php?file_id={jp.model_file_id}&type=model";
                }
                
                pd.from = false;

                // 下載第一張圖片
                if (jp.images != null && jp.images.Length > 0)
                {
                    string imageUrl = $"{apiBaseUrl}/gridfs_file.php?file_id={jp.images[0].file_id}&type=image";
                    UnityWebRequest imageRequest = UnityWebRequestTexture.GetTexture(imageUrl);
                    yield return imageRequest.SendWebRequest();

                    if (imageRequest.result == UnityWebRequest.Result.Success)
                    {
                        Texture2D texture = DownloadHandlerTexture.GetContent(imageRequest);
                        pd.productImage = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        pd.allSprites.Add(pd.productImage);
                    }
                    else
                    {
                        Debug.LogWarning($"下載圖片失敗: {imageUrl}");
                    }
                }

                productListManager.allProducts.Add(pd);
            }

            // 重新創建商品UI
            productListManager.CreateProductItems();
        }
    }

    private void BackupOriginalProducts()
    {
        originalProducts.Clear();
        if (productListManager != null && productListManager.allProducts != null)
        {
            originalProducts.AddRange(productListManager.allProducts);
            Debug.Log($"備份了 {originalProducts.Count} 個原始商品");
        }
    }

    private void RestoreOriginalProducts()
    {
        if (productListManager != null)
        {
            productListManager.allProducts.Clear();
            productListManager.allProducts.AddRange(originalProducts);
            ClearProductUI();
            productListManager.CreateProductItems();
            Debug.Log($"恢復了 {originalProducts.Count} 個原始商品");
        }
    }

    private IEnumerator ReloadAllProducts()
    {
        if (productListManager != null)
        {
            productListManager.allProducts.Clear();
            ClearProductUI();
            yield return StartCoroutine(productListManager.DownloadProductJson());
        }
    }

    private void ClearProductUI()
    {
        if (productListManager != null && productListManager.productContent != null)
        {
            foreach (Transform child in productListManager.productContent)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void ShowLoadingPanel(bool show)
    {
        if (searchLoadingPanel != null)
        {
            searchLoadingPanel.SetActive(show);
        }
        
        if (productListManager != null && productListManager.loadingPanel != null)
        {
            productListManager.loadingPanel.SetActive(show);
        }
    }

    private void ShowErrorMessage(string message)
    {
        HideStatusPanels();
        
        if (errorPanel != null)
        {
            errorPanel.SetActive(true);
            if (errorMessageText != null)
            {
                errorMessageText.text = message;
            }
        }
        
        Debug.LogError($"搜尋錯誤: {message}");
    }

    private void ShowNoResultsMessage(string message)
    {
        HideStatusPanels();
        
        // 直接清空商品列表，不顯示無結果面板
        ClearProductUI();
        
        // 清空 ProductListManager 的商品列表
        if (productListManager != null)
        {
            productListManager.allProducts.Clear();
        }
        
        Debug.Log($"搜尋無結果: {message}");
    }

    private void HideStatusPanels()
    {
        if (noResultsPanel != null)
        {
            noResultsPanel.SetActive(false);
        }
        
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }
    }

    private List<string> ParseProductIds(string idsJson)
    {
        try
        {
            string cleanJson = idsJson.Trim();
            
            if (cleanJson == "[]")
            {
                return new List<string>();
            }

            cleanJson = cleanJson.Substring(1, cleanJson.Length - 2);
            string[] idArray = cleanJson.Split(',');
            List<string> productIds = new List<string>();
            
            foreach (string id in idArray)
            {
                string cleanId = id.Trim().Trim('"');
                if (!string.IsNullOrEmpty(cleanId))
                {
                    productIds.Add(cleanId);
                }
            }
            
            return productIds;
        }
        catch (Exception e)
        {
            Debug.LogError($"解析商品 ID 失敗: {e.Message}, JSON: {idsJson}");
            return new List<string>();
        }
    }
}