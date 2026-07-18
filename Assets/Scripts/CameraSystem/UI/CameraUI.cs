using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RungTramTraSu.CameraSystem
{
    public class CameraUI : MonoBehaviour
    {
        public static CameraUI Instance { get; private set; }

        private GameObject canvasObject;
        private Canvas viewfinderCanvas;

        [Header("UI Elements")]
        private TextMeshProUGUI txtISO;
        private TextMeshProUGUI txtAperture;
        private TextMeshProUGUI txtShutter;
        private TextMeshProUGUI txtEV;
        private TextMeshProUGUI txtWB;
        private TextMeshProUGUI txtFocusMode;
        private TextMeshProUGUI txtBattery;
        private TextMeshProUGUI txtStorage;
        private TextMeshProUGUI txtFocalLength;
        private TextMeshProUGUI txtFocusDistance;
        private TextMeshProUGUI txtTutorialHint;

        private RectTransform focusDistanceBarFill;
        private GameObject gridOverlay;
        private Image focusLockBracket; // Active target lock display

        // Simulated Histogram Bars
        private List<RectTransform> histogramBars = new List<RectTransform>();
        private float histogramUpdateTimer = 0f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                BuildViewfinderUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetViewfinderActive(bool active)
        {
            if (canvasObject != null)
            {
                canvasObject.SetActive(active);
            }
        }

        private void Update()
        {
            if (canvasObject == null || !canvasObject.activeSelf) return;

            UpdateUIValues();
            UpdateHistogram();
        }

        private void UpdateUIValues()
        {
            CameraManager mgr = CameraManager.Instance;
            if (mgr == null) return;

            // 1. Text values
            if (txtISO != null) txtISO.text = $"ISO: {mgr.ExpSys.ISO}";
            if (txtAperture != null) txtAperture.text = $"F/{mgr.ExpSys.Aperture:F1}";
            if (txtShutter != null) txtShutter.text = mgr.ExpSys.CurrentShutterString;
            
            float ev = mgr.ExpSys.EVValue;
            if (txtEV != null) txtEV.text = $"EV: {(ev >= 0 ? "+" : "")}{ev:F1}";
            
            if (txtWB != null)
            {
                string wb = mgr.ExpSys.CurrentWBMode.ToString().ToUpper();
                if (mgr.ExpSys.CurrentWBMode == ExposureSystem.WhiteBalanceMode.CustomKelvin)
                {
                    wb = $"{mgr.ExpSys.CurrentKelvin}K";
                }
                txtWB.text = $"WB: {wb}";
            }

            if (txtFocusMode != null)
            {
                txtFocusMode.text = mgr.FocusSys.ActiveFocusMode == FocusMode.Manual ? "MF" : "AF";
                txtFocusMode.color = mgr.FocusSys.ActiveFocusMode == FocusMode.Manual ? Color.yellow : Color.green;
            }

            if (txtFocalLength != null) txtFocalLength.text = $"{mgr.LensSys.CurrentFocalLength:F0}mm";
            
            if (txtFocusDistance != null)
            {
                float dist = mgr.FocusSys.FocusDistance;
                txtFocusDistance.text = dist >= 70f ? "∞" : $"{dist:F1}m";
            }

            // Update focus bar fill
            if (focusDistanceBarFill != null)
            {
                float normalized = Mathf.InverseLerp(0.5f, 80f, mgr.FocusSys.FocusDistance);
                focusDistanceBarFill.anchorMax = new Vector2(normalized, 1f);
            }

            // Update battery and storage indicators from CameraManager
            if (txtBattery != null)
            {
                txtBattery.text = $"[BATT: {mgr.BatteryPercentage:F0}%]";
                txtBattery.color = mgr.BatteryPercentage < 20f ? Color.red : Color.white;
            }

            if (txtStorage != null)
            {
                txtStorage.text = $"[CARD: {mgr.AvailableStorage} SHOTS]";
                txtStorage.color = mgr.AvailableStorage < 5 ? Color.red : Color.white;
            }

            // Update active target lock bracket
            if (focusLockBracket != null)
            {
                Color targetColor;
                if (mgr.FocusSys.HasTargetLock && mgr.FocusSys.LockTarget != null)
                {
                    focusLockBracket.gameObject.SetActive(true);
                    
                    Vector3 screenPoint = Camera.main.WorldToScreenPoint(mgr.FocusSys.LockTarget.position);
                    focusLockBracket.rectTransform.position = screenPoint;
                    targetColor = Color.green;
                }
                else
                {
                    focusLockBracket.gameObject.SetActive(true);
                    focusLockBracket.rectTransform.anchoredPosition = Vector2.zero;
                    targetColor = new Color(0.9f, 0.9f, 0.9f, 0.8f);
                }

                // Propagate targetColor to all border lines and crosshair
                focusLockBracket.color = Color.clear; // Hollow center
                foreach (Image img in focusLockBracket.GetComponentsInChildren<Image>(true))
                {
                    if (img != focusLockBracket)
                    {
                        img.color = targetColor;
                    }
                }
            }

            // Update tutorial lesson overlay
            if (txtTutorialHint != null)
            {
                if (TutorialManager.Instance != null && TutorialManager.Instance.CurrentLesson != TutorialManager.LessonStep.Finished)
                {
                    txtTutorialHint.text = TutorialManager.Instance.GetLessonHint();
                }
                else
                {
                    txtTutorialHint.text = mgr.IsManualMode ? "CHẾ ĐỘ THỦ CÔNG (Tự phơi sáng)" : "CHẾ ĐỘ TỰ ĐỘNG (TAB để khóa nét)";
                }
            }
        }

        private void UpdateHistogram()
        {
            histogramUpdateTimer += Time.deltaTime;
            if (histogramUpdateTimer < 0.15f) return; 
            histogramUpdateTimer = 0f;

            CameraManager mgr = CameraManager.Instance;
            if (mgr == null || histogramBars.Count == 0) return;

            float err = mgr.ExpSys.CalculateLuminanceDeviation();
            float centerPeak = 7 - Mathf.RoundToInt(err * 3f);
            centerPeak = Mathf.Clamp(centerPeak, 1, 13);

            for (int i = 0; i < histogramBars.Count; i++)
            {
                float distToPeak = Mathf.Abs(i - centerPeak);
                float heightFactor = Mathf.Clamp01(1f - (distToPeak / 7f));
                
                float fluctuation = Random.Range(-0.06f, 0.06f);
                float finalHeight = Mathf.Clamp01(heightFactor * 0.9f + fluctuation + 0.1f);

                histogramBars[i].localScale = new Vector3(1f, finalHeight, 1f);
            }
        }

        private void BuildViewfinderUI()
        {
            canvasObject = new GameObject("[CameraViewfinderCanvas]");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = CreateRT("ViewfinderPanel", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = Color.clear;
            panelImg.raycastTarget = false;

            // --- Top HUD Bar ---
            GameObject topBar = CreateRT("TopBar", panel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(0f, 60f));
            txtBattery = CreateText("BatteryText", topBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(250f, 40f), "[BATT: 100%]", 22f, TextAlignmentOptions.Left);
            txtStorage = CreateText("StorageText", topBar.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-50f, 0f), new Vector2(250f, 40f), "[CARD: 50 SHOTS]", 22f, TextAlignmentOptions.Right);

            // --- Bottom DSLR Settings Strip ---
            GameObject bottomBar = CreateRT("BottomBar", panel.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(0f, 80f));
            Image stripBg = bottomBar.AddComponent<Image>();
            stripBg.color = new Color(0.05f, 0.05f, 0.05f, 0.65f);

            float blockWidth = 200f;
            float startOffset = 180f;

            txtISO = CreateText("ISOText", bottomBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(startOffset, 0f), new Vector2(blockWidth, 50f), "ISO: 100", 22f, TextAlignmentOptions.Center);
            txtAperture = CreateText("ApertureText", bottomBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(startOffset + blockWidth, 0f), new Vector2(blockWidth, 50f), "F/4.0", 22f, TextAlignmentOptions.Center);
            txtShutter = CreateText("ShutterText", bottomBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(startOffset + blockWidth * 2f, 0f), new Vector2(blockWidth, 50f), "1/250", 22f, TextAlignmentOptions.Center);
            txtEV = CreateText("EVText", bottomBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(startOffset + blockWidth * 3f, 0f), new Vector2(blockWidth, 50f), "EV: 0.0", 22f, TextAlignmentOptions.Center);
            txtWB = CreateText("WBText", bottomBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(startOffset + blockWidth * 4f, 0f), new Vector2(blockWidth, 50f), "WB: AUTO", 22f, TextAlignmentOptions.Center);
            txtFocusMode = CreateText("FocusModeText", bottomBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(startOffset + blockWidth * 5f, 0f), new Vector2(blockWidth, 50f), "AF", 22f, TextAlignmentOptions.Center);
            txtFocalLength = CreateText("LensText", bottomBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(startOffset + blockWidth * 6f, 0f), new Vector2(blockWidth, 50f), "50mm", 22f, TextAlignmentOptions.Center);

            // --- Focus Distance Bar ---
            GameObject focusBarBg = CreateRT("FocusBarBg", panel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(400f, 8f));
            Image focusBarBgImg = focusBarBg.AddComponent<Image>();
            focusBarBgImg.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);

            GameObject focusBarFill = CreateRT("FocusBarFill", focusBarBg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            focusDistanceBarFill = focusBarFill.GetComponent<RectTransform>();
            Image focusBarFillImg = focusBarFill.AddComponent<Image>();
            focusBarFillImg.color = Color.yellow;

            txtFocusDistance = CreateText("FocusDistanceVal", focusBarBg.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(60f, 0f), new Vector2(80f, 30f), "10.0m", 18f, TextAlignmentOptions.Left);
            CreateText("FocusLabel", focusBarBg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-70f, 0f), new Vector2(100f, 30f), "FOCUS DIST:", 18f, TextAlignmentOptions.Right);

            // --- Center Focus Bracket ---
            GameObject bracketObj = CreateRT("FocusBracket", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            focusLockBracket = bracketObj.AddComponent<Image>();
            focusLockBracket.color = Color.clear; // Hollow center!
            
            CreateBorderLine("TL_H", bracketObj.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(15f, -2f), new Vector2(30f, 4f));
            CreateBorderLine("TL_V", bracketObj.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(2f, -15f), new Vector2(4f, 30f));

            CreateBorderLine("TR_H", bracketObj.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-15f, -2f), new Vector2(30f, 4f));
            CreateBorderLine("TR_V", bracketObj.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-2f, -15f), new Vector2(4f, 30f));

            CreateBorderLine("BL_H", bracketObj.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(15f, 2f), new Vector2(30f, 4f));
            CreateBorderLine("BL_V", bracketObj.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(2f, 15f), new Vector2(4f, 30f));

            CreateBorderLine("BR_H", bracketObj.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-15f, 2f), new Vector2(30f, 4f));
            CreateBorderLine("BR_V", bracketObj.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-2f, 15f), new Vector2(4f, 30f));

            // Center Crosshair lines (+ symbol in the middle)
            CreateBorderLine("Center_H", bracketObj.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 4f));
            CreateBorderLine("Center_V", bracketObj.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 24f));

            // --- Rule of Thirds Grid Overlay ---
            gridOverlay = CreateRT("GridOverlay", panel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            gridOverlay.SetActive(true);
            
            CreateGridLine("LineH1", gridOverlay.transform, new Vector2(0f, 0.333f), new Vector2(1f, 0.333f), Vector2.zero, new Vector2(0f, 1f));
            CreateGridLine("LineH2", gridOverlay.transform, new Vector2(0f, 0.666f), new Vector2(1f, 0.666f), Vector2.zero, new Vector2(0f, 1f));
            CreateGridLine("LineV1", gridOverlay.transform, new Vector2(0.333f, 0f), new Vector2(0.333f, 1f), Vector2.zero, new Vector2(1f, 0f));
            CreateGridLine("LineV2", gridOverlay.transform, new Vector2(0.666f, 0f), new Vector2(0.666f, 1f), Vector2.zero, new Vector2(1f, 0f));

            // --- Histogram Panel (Bottom-Left) ---
            GameObject histoPanel = CreateRT("HistogramPanel", panel.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(120f, 220f), new Vector2(180f, 100f));
            Image histBg = histoPanel.AddComponent<Image>();
            histBg.color = new Color(0f, 0f, 0f, 0.45f);

            int barCount = 15;
            float barSpacing = 12f;
            for (int i = 0; i < barCount; i++)
            {
                float xPos = -90f + (i * barSpacing) + 12f;
                GameObject bar = CreateRT($"Bar_{i}", histoPanel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(xPos, 5f), new Vector2(8f, 90f));
                Image barImg = bar.AddComponent<Image>();
                barImg.color = new Color(0.9f, 0.9f, 0.9f, 0.7f);
                
                RectTransform rt = bar.GetComponent<RectTransform>();
                rt.pivot = new Vector2(0.5f, 0f);
                
                histogramBars.Add(rt);
            }

            // --- Tutorial Objective Overlay ---
            GameObject tutObj = CreateRT("TutorialObjectivePanel", panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(700f, 60f));
            Image tutBg = tutObj.AddComponent<Image>();
            tutBg.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);
            txtTutorialHint = CreateText("TutorialHintText", tutObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, "Mục tiêu hướng dẫn...", 20f, TextAlignmentOptions.Center);
            txtTutorialHint.fontStyle = FontStyles.Bold;

            canvasObject.SetActive(false);
        }

        private static GameObject CreateRT(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return go;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, string initText, float fontSize, TextAlignmentOptions align)
        {
            GameObject go = CreateRT(name, parent, anchorMin, anchorMax, anchoredPos, sizeDelta);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = initText;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Normal;
            return tmp;
        }

        private void CreateBorderLine(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject line = CreateRT(name, parent, anchorMin, anchorMax, anchoredPos, sizeDelta);
            Image img = line.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f, 0.8f);
        }

        private void CreateGridLine(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject line = CreateRT(name, parent, anchorMin, anchorMax, anchoredPos, sizeDelta);
            Image img = line.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.22f);
        }
    }
}
