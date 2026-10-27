namespace BeachBoatStory
{
    /// <summary>One vocabulary for the objective card, direction arrow and battle notices.</summary>
    public static class RiverBattleObjective
    {
        public static bool Counterattack=>EnemyFleetDirector.Instance?.AmbushStarted==true||
            BachDangBattleTimeline.Instance?.CurrentMoment==BachDangBattleTimeline.BattleMoment.Ambush;
        public static bool WaitingForEbb=>!Counterattack&&(EnemyFleetDirector.Instance?.EbbStarted==true||
            BachDangBattleTimeline.Instance?.CurrentMoment==BachDangBattleTimeline.BattleMoment.TideFalling||
            BachDangBattleTimeline.Instance?.CurrentMoment==BachDangBattleTimeline.BattleMoment.FleetInsideStakeField);
        static RiverFleetPursuit Pursuit=>EnemyFleetDirector.Instance?.Pursuit;
        public static string Title=>Counterattack?"Hồi 3 • Phản công":WaitingForEbb?"Hồi 3 • Chờ nước rút":
            Pursuit?.IsPursuing==true?"Hồi 3 • Dẫn địch vào bãi cọc":"Hồi 3 • Chèo ra nhử địch";
        public static string Description
        {
            get
            {
                var fleet=EnemyFleetDirector.Instance;
                if(Counterattack)return $"- Cọc đã lộ — cùng đồng minh tiêu diệt quân địch.\n- Q: kiếm • Chuột phải: ngắm cung • SPACE: leo thuyền.\n- Thuyền đắm {BachDangStoryManager.Instance?.destroyedEnemyShips??0}/{fleet?.ShipCount??0} • Địch còn {fleet?.RemainingEnemies??0}.";
                if(WaitingForEbb)return "- Địch đã vào trận địa. Giữ ngoài bãi cọc.\n- Chờ cọc lộ và lệnh phản công của Ngô Quyền.\n- Chưa cần áp sát hay leo thuyền địch.";
                if(Pursuit?.HasEnteredTrap==true)return $"- Đã dẫn địch vào bẫy. Chờ đủ {fleet?.ShipsInside??0}/{fleet?.ShipCount??0} thuyền vào sâu.\n- Rời bãi cọc theo mũi tên, chờ nước rút.\n- Chưa tấn công; chờ lệnh Ngô Quyền.";
                if(Pursuit?.IsPursuing==true)return "- Địch đang đuổi theo! Quay thuyền về bãi cọc theo mũi tên.\n- Chèo xuyên qua trận địa, giữ khoảng cách với thuyền lớn.\n- Chưa tấn công; dụ cả đội hình vào sâu.";
                return "- Chèo ra tiếp cận hạm đội Nam Hán ở ngoài xa theo mũi tên.\n- Khi địch hô truy đuổi, quay lại dẫn chúng vào bãi cọc.\n- Chưa tấn công; giữ thuyền nguyên vẹn.";
            }
        }
        public static string DirectionTitle=>Counterattack?"Tiêu diệt lính Nam Hán":WaitingForEbb?"Giữ ngoài bãi cọc • Chờ nước rút":
            Pursuit?.IsPursuing==true?"Dẫn địch qua bãi cọc • Tiếp tục chèo":"Tiếp cận hạm đội • Chèo ra nhử địch";
        public static string ControlHint=>Counterattack?"Cung/kiếm • SPACE leo tàu":"WASD chèo • Chưa tấn công";
    }
}
