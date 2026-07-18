using UnityEngine;
using UnityEngine.SceneManagement;

namespace RungTramTraSu.CameraSystem
{
    public class PhotoValidator : MonoBehaviour
    {
        private static PhotoValidator instance;
        public static PhotoValidator Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("PhotoValidator");
                    instance = go.AddComponent<PhotoValidator>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        // Validates if the captured target meets all quest and technical requirements
        public bool ValidatePhoto(
            WildlifeDetector.DetectedTarget target,
            float blurFactor,
            string activeQuestCategory,
            Transform currentQuestTarget,
            out string failReason)
        {
            failReason = "";

            // 1. Occlusion Check
            if (target.isOccluded)
            {
                // Sunset and Mango Tree quests have no occlusion check
                bool ignoreOcclusion = target.displayName == "SunsetQuestTarget" || 
                                       target.displayName.Contains("Hoàng Hôn") ||
                                       target.displayName.Contains("Mango") ||
                                       target.displayName.Contains("Xoài");

                if (!ignoreOcclusion)
                {
                    failReason = "Chủ thể bị che khuất bởi vật cản phía trước!";
                    return false;
                }
            }

            // 2. Minimum screen coverage check
            float minCoverage = 0.25f; // Lowered from 0.8f to be more forgiving
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Phase2"))
            {
                minCoverage = 0.12f; // Fast flying birds can be very small
            }

            if (target.screenCoverage < minCoverage && !target.displayName.Contains("Hoàng Hôn"))
            {
                failReason = "Chủ thể quá xa hoặc quá nhỏ trong khung hình. Tiến lại gần hơn hoặc Zoom lớn lên!";
                return false;
            }


            // 3. Focus Check
            if (blurFactor > 0.65f)
            {
                failReason = "Hình ảnh quá mờ! Hãy điều chỉnh lấy nét (nút Q/E hoặc Tab) cho chuẩn.";
                return false;
            }

            // 4. Quest specific validation


            if (sceneName.Contains("Phase1"))
            {
                if (!target.displayName.Contains("Mango") && !target.displayName.Contains("Xoài"))
                {
                    failReason = "Nhiệm vụ: Chụp ảnh Cây Xoài Cổ Thụ!";
                    return false;
                }
            }
            else if (sceneName.Contains("Phase2"))
            {
                bool isBird = target.category == "Birds" || 
                              target.displayName.Contains("Chim") || target.displayName.Contains("Cò") || 
                              target.displayName.Contains("Sếu") || target.displayName.Contains("Diệc") || 
                              target.displayName.Contains("Vạc") || target.displayName.Contains("Le le") || 
                              target.displayName.Contains("Én") || target.displayName.Contains("Bìm bịp") ||
                              target.displayName.Contains("Già") || target.displayName.Contains("cộc") ||
                              target.displayName.Contains("Trích") || target.displayName.Contains("Điêng") ||
                              target.displayName.Contains("cá");
                if (!isBird)
                {
                    failReason = "Nhiệm vụ: Chụp các loài chim đang bay!";
                    return false;
                }
            }

            else if (sceneName.Contains("Phase4"))
            {
                if (!string.IsNullOrEmpty(activeQuestCategory) && activeQuestCategory.StartsWith("Phase4_"))
                {
                    string targetSpeciesString = activeQuestCategory.Replace("Phase4_", "").ToLower(); // e.g. "stork", "duck"
                    
                    string candidateName = target.displayName.ToLower();
                    bool match = false;
                    if (targetSpeciesString == "stork" && (candidateName.Contains("cò") || candidateName.Contains("stork"))) match = true;
                    else if (targetSpeciesString == "duck" && (candidateName.Contains("vịt") || candidateName.Contains("duck"))) match = true;
                    else if (targetSpeciesString == "fish" && (candidateName.Contains("cá") || candidateName.Contains("fish"))) match = true;
                    else if (targetSpeciesString == "butterfly" && (candidateName.Contains("bướm") || candidateName.Contains("butterfly"))) match = true;
                    else if (targetSpeciesString == "snake" && (candidateName.Contains("rắn") || candidateName.Contains("snake"))) match = true;

                    if (!match)
                    {
                        failReason = $"Mục tiêu hiện tại là chụp {GetVnAnimalNameEnglish(targetSpeciesString)}. Tấm hình này chụp nhầm loài khác rồi!";
                        return false;
                    }
                }
            }
            else if (sceneName.Contains("Phase5"))
            {
                if (!target.displayName.Contains("Hoàng Hôn") && !target.displayName.Contains("Sunset"))
                {
                    failReason = "Nhiệm vụ: Chụp ảnh Hoàng hôn Rừng Tràm!";
                    return false;
                }
            }

            return true;
        }

        private string GetVnAnimalNameEnglish(string eng)
        {
            switch (eng)
            {
                case "stork": return "Cò Trắng";
                case "duck": return "Vịt Trời";
                case "fish": return "Cá Lóc";
                case "butterfly": return "Bướm Hoa Súng";
                case "snake": return "Rắn Nước";
                default: return "động vật thích hợp";
            }
        }
    }
}
