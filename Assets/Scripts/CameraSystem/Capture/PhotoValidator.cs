using UnityEngine;
using UnityEngine.SceneManagement;

namespace RungTramTraSu.CameraSystem
{
    public class PhotoValidator : MonoBehaviour
    {
        public static PhotoValidator Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
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
            // If the animal is too far (e.g. less than 1% screen area), reject it
            if (target.screenCoverage < 0.8f && !target.displayName.Contains("Hoàng Hôn"))
            {
                failReason = "Chủ thể quá xa hoặc quá nhỏ trong khung hình. Tiến lại gần hơn!";
                return false;
            }

            // 3. Focus Check
            // A extremely blurry photo (blurFactor > 0.6) is rejected
            if (blurFactor > 0.65f)
            {
                failReason = "Hình ảnh quá mờ! Hãy điều chỉnh lấy nét (nút Q/E hoặc Tab) cho chuẩn.";
                return false;
            }

            // 4. Quest specific validation
            string sceneName = SceneManager.GetActiveScene().name;

            if (sceneName.Contains("Phase1"))
            {
                // Phase 1 requires the Ancient Mango Tree
                if (!target.displayName.Contains("Mango") && !target.displayName.Contains("Xoài"))
                {
                    failReason = "Nhiệm vụ: Chụp ảnh Cây Xoài Cổ Thụ!";
                    return false;
                }
            }
            else if (sceneName.Contains("Phase2"))
            {
                // Phase 2 requires birds (either standard bird from checkpoint or Sarus crane)
                bool isBird = target.displayName.Contains("Chim") || target.displayName.Contains("Cò") || 
                             target.displayName.Contains("Sếu") || target.displayName.Contains("Diệc") || 
                             target.displayName.Contains("Vạc") || target.displayName.Contains("Le le") || 
                             target.displayName.Contains("Én") || target.displayName.Contains("Bìm bịp");
                if (!isBird)
                {
                    failReason = "Nhiệm vụ: Chụp các loài chim đang bay!";
                    return false;
                }
            }
            else if (sceneName.Contains("Phase4"))
            {
                // Phase 4 requires the specific animal from the current list (Stork, Duck, Fish, Butterfly, Snake)
                // The category in Phase4Manager is set to e.g. "Phase4_Stork"
                if (!string.IsNullOrEmpty(activeQuestCategory) && activeQuestCategory.StartsWith("Phase4_"))
                {
                    string targetSpeciesString = activeQuestCategory.Replace("Phase4_", "").ToLower(); // e.g. "stork", "duck"
                    
                    // Map display name to species keyword
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
                // Phase 5 requires the sunset
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
