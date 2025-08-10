using UnityEngine; 
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using ARFurniture;

#region 產品 JSON 資料結構定義

// 產品資料物件，用來存放解析後的產品資訊
[System.Serializable]
public class ProductData
{
    public string modelURL;           // 3D 模型檔案的位址
    public string productName;        // 產品名稱
    public float price;               // 價格
    public string url;                // 產品連結
    public string otherInfo;          // 產品描述資訊
    public Sprite productImage;       // 產品主要顯示圖片
    public List<Sprite> allSprites = new List<Sprite>();  // 所有圖片的 Sprite 列表
    public string sizeOptions;        // 尺寸資訊
    public bool from;                 // 區分來源用的旗標，UI1 預設為 false
    public string productId;          // 產品 ID
}

// 更新為 MongoDB API 回應結構
[System.Serializable]
public class APIResponse
{
    public bool success;              // API 是否成功
    public JSONProduct[] products;    // 產品陣列
    public int total_count;           // 總數量
    public string message;            // 回應訊息
}

// 更新為新的 MongoDB 產品資料結構
[System.Serializable]
public class JSONProduct
{
    public ProductId _id;             // MongoDB ObjectId
    public string category;           // 產品類別
    public string name;               // 產品名稱
    public float price;               // 直接為 float 型態
    public string url;                // 產品連結
    public string description;        // 產品描述
    public string brand;              // 品牌
    public string width;              // 寬度
    public string height;             // 高度
    public string depth;              // 深度
    public string model_file_id;   // 模型檔案 ID
    public ImageInfo[] images;        // 圖片資訊陣列
    public string status;             // 狀態
    public string product_id;         // 產品 ID
}

// MongoDB ObjectId 結構
[System.Serializable]
public class ProductId
{
    public string oid;                // MongoDB ObjectId 字串

    // 隱式轉換為字串
    public static implicit operator string(ProductId id)
    {
        return id?.oid ?? "";
    }
}

// 圖片資訊結構
[System.Serializable]
public class ImageInfo
{
    public string file_id;            // 圖片檔案 ID
    public string filename;           // 檔案名稱
    public string original_filename;  // 原始檔案名稱
    public string content_type;       // 內容類型
    public int image_index;           // 圖片索引
    public string url;                // 圖片 URL（由 API 提供）
}

// JSON 輔助解析工具，用於處理 JSON 陣列資料
public static class JsonHelper
{
    // 直接解析 API 回應
    public static APIResponse FromJson(string json)
    {
        return JsonUtility.FromJson<APIResponse>(json);
    }

    // 保留原有的陣列解析方法以備用
    public static T[] FromJsonArray<T>(string json)
    {
        string newJson = "{ \"array\": " + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper.array;
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] array;
    }
}

#endregion

public class ProductListManager : MonoBehaviour
{
    public static ProductListManager Instance { get; private set; }
    
    private string apiBaseUrl = "http://140.127.114.38:5008/php";  // API 基礎 URL
    
    [Header("產品項目 Prefab")]
    public GameObject productItemPrefab;

    [Header("產品列表容器")]
    public Transform productContent;

    [Header("產品資訊面板")]
    public GameObject panelRoot;

    [Header("其他 UI 按鈕")]
    public Button listBtn;
    public GameObject listBtnImage;
    public bool activeSelf = true;
    public Sprite upSprite;
    public Sprite downSprite;

    public List<ProductData> allProducts;
    public ModelLoader1 modelLoader1;

    [Header("商品加載")]
    public GameObject loadingPanel;

    [Header("網格佈局設置")]
    [SerializeField] private Vector2 cellSize = new Vector2(300f, 350f);
    [SerializeField] private Vector2 spacing = new Vector2(20f, 20f);
    [SerializeField] private int paddingLeft = 20;
    [SerializeField] private int paddingRight = 20;
    [SerializeField] private int paddingTop = 20;
    [SerializeField] private int paddingBottom = 20;

    private int totalProductCount = 0;
    private int downloadedImageCount = 0;

    void Start()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }
        listBtn.image.sprite = downSprite;
        listBtn.onClick.AddListener(OnClickToggleImage);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        allProducts = new List<ProductData>();

        // 使用新的 API 端點下載產品資料
        StartCoroutine(DownloadProductJson());
    }

    // 解析 MongoDB API 回應的產品資料
    void ParseJsonProducts(string jsonText)
    {
        try
        {
            APIResponse response = JsonHelper.FromJson(jsonText);
            
            if (!response.success)
            {
                Debug.LogError("API 回應失敗: " + response.message);
                if (loadingPanel != null)
                {
                    loadingPanel.SetActive(false);
                }
                return;
            }

            JSONProduct[] products = response.products;
            totalProductCount = products.Length;
            downloadedImageCount = 0;
            
            Debug.Log($"成功載入 {totalProductCount} 個商品");
            
            foreach (var jp in products)
            {
                ProductData pd = new ProductData();
                pd.productName = jp.name;
                pd.price = jp.price;  // 直接使用 float 價格
                pd.url = jp.url;
                pd.otherInfo = jp.description;
                pd.productId = jp.product_id;
                
                // 組合尺寸資訊 - 只顯示有資料的部分
                List<string> sizeComponents = new List<string>();
                if (!string.IsNullOrEmpty(jp.depth) && jp.depth != "0")
                    sizeComponents.Add($"深{jp.depth}");
                if (!string.IsNullOrEmpty(jp.width) && jp.width != "0")
                    sizeComponents.Add($"寬{jp.width}");
                if (!string.IsNullOrEmpty(jp.height) && jp.height != "0")
                    sizeComponents.Add($"高{jp.height}");
                
                pd.sizeOptions = sizeComponents.Count > 0 ? string.Join(" x ", sizeComponents) + " (cm))" : "尺寸資訊不完整";
                
                
                // 建構模型 URL
                if (jp.model_file_id != null && !string.IsNullOrEmpty(jp.model_file_id))
                {
                    Debug.Log($"模型檔案 ID: {jp.model_file_id}");
                    pd.modelURL = $"{apiBaseUrl}/gridfs_file.php?file_id={jp.model_file_id}&type=model";
                }
                
                pd.from = false;
                allProducts.Add(pd);
                
                // 下載新格式的圖片
                StartCoroutine(DownloadImagesForProduct(jp.images, pd));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("解析 JSON 失敗: " + e.Message);
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }
        }
    }

    // 處理新的圖片格式
    IEnumerator DownloadImagesForProduct(ImageInfo[] imageInfos, ProductData pd)
    {
        if (imageInfos == null || imageInfos.Length == 0)
        {
            downloadedImageCount++;
            CheckAllProductsCompletion();
            yield break;
        }

        // 先下載第一張圖片作為主要圖片
        ImageInfo mainImageInfo = imageInfos[0];
        string mainImageUrl = $"{apiBaseUrl}/gridfs_file.php?file_id={mainImageInfo.file_id}&type=image";
        
        UnityWebRequest mainRequest = UnityWebRequestTexture.GetTexture(mainImageUrl);
        yield return mainRequest.SendWebRequest();

        bool mainImageDownloaded = false;
        
        if (mainRequest.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(mainRequest);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            
            pd.productImage = sprite;
            pd.allSprites.Add(sprite);
            mainImageDownloaded = true;
            Debug.Log($"成功下載主圖: {pd.productName}");
        }
        else
        {
            Debug.LogWarning($"下載主要圖片失敗 - 產品: {pd.productName}, URL: {mainImageUrl}, 錯誤: {mainRequest.error}");
            
            // 如果第一張下載失敗，嘗試其他圖片
            for (int i = 1; i < imageInfos.Length; i++)
            {
                string backupImageUrl = $"{apiBaseUrl}/gridfs_file.php?file_id={imageInfos[i].file_id}&type=image";
                UnityWebRequest backupRequest = UnityWebRequestTexture.GetTexture(backupImageUrl);
                yield return backupRequest.SendWebRequest();
                
                if (backupRequest.result == UnityWebRequest.Result.Success)
                {
                    Texture2D backupTexture = DownloadHandlerTexture.GetContent(backupRequest);
                    Sprite backupSprite = Sprite.Create(backupTexture, new Rect(0, 0, backupTexture.width, backupTexture.height), new Vector2(0.5f, 0.5f));
                    
                    pd.productImage = backupSprite;
                    pd.allSprites.Add(backupSprite);
                    mainImageDownloaded = true;
                    Debug.Log($"使用備用圖片成功: {pd.productName}");
                    break;
                }
            }
        }
        
        if (!mainImageDownloaded)
        {
            Debug.LogWarning($"所有圖片下載失敗，產品: {pd.productName}");
        }
        
        // 在背景下載其餘圖片
        StartCoroutine(DownloadRemainingImages(imageInfos, pd));
        
        downloadedImageCount++;
        CheckAllProductsCompletion();
    }

    // 下載剩餘圖片
    IEnumerator DownloadRemainingImages(ImageInfo[] imageInfos, ProductData pd)
    {
        for (int i = 1; i < imageInfos.Length; i++)
        {
            if (pd.allSprites.Count > i)
                continue;
            
            string imageUrl = $"{apiBaseUrl}/gridfs_file.php?file_id={imageInfos[i].file_id}&type=image";
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                pd.allSprites.Add(sprite);
            }
            else
            {
                Debug.LogWarning($"下載額外圖片失敗: {imageUrl}");
            }
        }
    }

    private void CheckAllProductsCompletion()
    {
        if (downloadedImageCount >= totalProductCount)
        {
            CreateProductItems();
            
            Debug.Log("所有商品載入完成");

            StartCoroutine(HideLoadingPanelCoroutine());
        }
    }

    private IEnumerator HideLoadingPanelCoroutine()
    {
        yield return new WaitForEndOfFrame();
        
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
            
            // 再次檢查是否成功隱藏
            yield return new WaitForEndOfFrame();
            
            if (loadingPanel.activeInHierarchy)
            {
                // 如果還是沒有隱藏，再次嘗試
                loadingPanel.SetActive(false);
            }
        }
    }

    // 使用新的 API 端點
    public IEnumerator DownloadProductJson()
    {
        string jsonUrl = $"{apiBaseUrl}/products.php?action=get_list";
        Debug.Log($"正在從 API 載入商品資料: {jsonUrl}");
        
        UnityWebRequest www = UnityWebRequest.Get(jsonUrl);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"下載商品資料失敗: {www.error}");
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }
        }
        else
        {
            string jsonText = www.downloadHandler.text;
            Debug.Log($"收到 API 回應: {jsonText.Substring(0, Mathf.Min(200, jsonText.Length))}...");
            ParseJsonProducts(jsonText);
        }
    }

    private void SetupGridLayout()
    {
        var existingVertical = productContent.GetComponent<VerticalLayoutGroup>();
        if (existingVertical != null)
        {
            DestroyImmediate(existingVertical);
        }

        var existingHorizontal = productContent.GetComponent<HorizontalLayoutGroup>();
        if (existingHorizontal != null)
        {
            DestroyImmediate(existingHorizontal);
        }

        var existingGrid = productContent.GetComponent<GridLayoutGroup>();
        if (existingGrid != null)
        {
            DestroyImmediate(existingGrid);
        }

        GridLayoutGroup gridLayout = productContent.gameObject.AddComponent<GridLayoutGroup>();
        
        gridLayout.cellSize = cellSize;
        gridLayout.spacing = spacing;
        gridLayout.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 2;
        gridLayout.childAlignment = TextAnchor.UpperCenter;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
    }

    public void CreateProductItems()
    {
        SetupGridLayout();

        foreach (var pd in allProducts)
        {
            GameObject itemObj = Instantiate(productItemPrefab, productContent);
            
            RectTransform btnRect = itemObj.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(400f, 400f);

            var productImageTransform = itemObj.transform.Find("ProductImage")?.GetComponent<Image>();
            var nameTxt = itemObj.transform.Find("NameText")?.GetComponentInChildren<Text>();
            var priceTxt = itemObj.transform.Find("PriceText")?.GetComponentInChildren<TextMeshProUGUI>();
            var arBtn = itemObj.transform.Find("ViewARButton")?.GetComponent<Button>();
            var infoBtn = itemObj.transform.Find("InfoButton")?.GetComponent<Button>();
            
            if (productImageTransform != null)
            {
                Transform imgTransform = productImageTransform.transform.Find("Image");
                if (imgTransform != null)
                {
                    Image img = imgTransform.GetComponent<Image>();
                    if (img != null && pd.productImage != null)
                    {
                        img.sprite = pd.productImage;

                        float fixedSize = 400f; 
                        float spriteWidth = pd.productImage.rect.width;
                        float spriteHeight = pd.productImage.rect.height;
                        float calculatedWidth = fixedSize * (spriteWidth / spriteHeight);
                        float calculatedHeight = fixedSize * (spriteHeight / spriteWidth);

                        RectTransform imgRect = img.GetComponent<RectTransform>();
                        if (calculatedWidth > fixedSize)
                        {
                            imgRect.sizeDelta = new Vector2(fixedSize, calculatedHeight);
                        }
                        else
                        {
                            imgRect.sizeDelta = new Vector2(calculatedWidth, fixedSize);
                        }
                        imgRect.anchoredPosition = Vector2.zero;
                    }
                }
            }

            if (nameTxt != null)
                nameTxt.text = pd.productName;
            if (priceTxt != null)
                priceTxt.text = "$" + pd.price.ToString("F0");  // 格式化價格顯示

            if (infoBtn != null)
            {
                ProductData capturedPd = pd;
                infoBtn.onClick.AddListener(() =>
                {
                    OnClickToggleImage();
                    InfoPanelController.Instance.ShowProductInfo(capturedPd);
                });
            }

            if (arBtn != null)
            {
                ProductData capturedPd = pd;
                arBtn.onClick.AddListener(() =>
                {
                    OnClickToggleImage();
                    modelLoader1.SetModelToLoad(capturedPd.modelURL, capturedPd);
                });
            }
        }
    }

    public void OnClickToggleImage()
    {
        Sprite currentSprite = listBtn.image.sprite;

        if (currentSprite == upSprite)
        {
            listBtn.image.sprite = downSprite;
            panelRoot.SetActive(false);
        }
        else
        {
            listBtn.image.sprite = upSprite;
            panelRoot.SetActive(true);
        }
    }

    public void OnClicklist(){
        listBtnImage.SetActive(!listBtnImage.activeSelf);
    }
}

