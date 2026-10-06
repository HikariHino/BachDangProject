using UnityEngine;
using UnityEditor;

public static class MultiAngleCapture
{
    public static void CaptureAllViews()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // 1. Góc nhìn Chủ Tướng Ngô Quyền phóng tầm mắt ra dòng sông Bạch Đằng
        cam.transform.position = new Vector3(-814f, 19.5f, 320f);
        cam.transform.LookAt(new Vector3(-770f, 16.5f, 320f));
        ScreenCapture.CaptureScreenshot("A:\\FPT subject\\New folder\\BachDangProject\\view_1_ngo_quyen.png");

        // 2. Cận cảnh Trận địa cọc & Thuyền nhử giặc
        cam.transform.position = new Vector3(-430f, 22f, 280f);
        cam.transform.LookAt(new Vector3(-350f, 14.2f, 300f));
        ScreenCapture.CaptureScreenshot("A:\\FPT subject\\New folder\\BachDangProject\\view_2_battle_stakes.png");

        // 3. Toàn cảnh Sa bàn 6000m từ trên cao
        cam.transform.position = new Vector3(-550f, 150f, -20f);
        cam.transform.LookAt(new Vector3(-380f, 14f, 300f));
        ScreenCapture.CaptureScreenshot("A:\\FPT subject\\New folder\\BachDangProject\\view_3_tactical_birds_eye.png");

        // Đặt lại camera về góc nhìn bãi cọc mặc định
        cam.transform.position = new Vector3(-460f, 24f, 300f);
        cam.transform.LookAt(new Vector3(-350f, 14.2f, 300f));

        Debug.Log("[MultiAngleCapture] Đã chụp thành công 3 góc nhìn điện ảnh mới!");
    }
}
