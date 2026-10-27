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
        public static string Title=>Counterattack?"Hồi 3 • Phản công":WaitingForEbb?"Hồi 3 • Chờ nước rút":"Hồi 3 • Nhử địch vào trận địa";
        public static string Description
        {
            get
            {
                var fleet=EnemyFleetDirector.Instance;
                if(Counterattack)return $"- Cọc đã lộ — cùng đồng minh tiêu diệt quân địch.\n- Q: kiếm • Chuột phải: ngắm cung • SPACE: leo thuyền.\n- Thuyền đắm {BachDangStoryManager.Instance?.destroyedEnemyShips??0}/{fleet?.ShipCount??0} • Địch còn {fleet?.RemainingEnemies??0}.";
                if(WaitingForEbb)return "- Địch đã vào trận địa. Giữ ngoài bãi cọc.\n- Chờ cọc lộ và lệnh phản công của Ngô Quyền.\n- Chưa cần áp sát hay leo thuyền địch.";
                return $"- Tới vùng nhử bên ngoài bãi cọc.\n- Chờ hạm đội Nam Hán tiến vào vòng vây: {fleet?.ShipsInside??0}/{fleet?.ShipCount??0} thuyền.\n- Chưa tấn công; chờ nước rút và lệnh phản công.";
            }
        }
        public static string DirectionTitle=>Counterattack?"Tiêu diệt lính Nam Hán":WaitingForEbb?"Giữ ngoài bãi cọc • Chờ nước rút":"Vùng nhử địch • Chờ hạm đội vào vòng vây";
        public static string ControlHint=>Counterattack?"Cung/kiếm • SPACE leo tàu":"Giữ khoảng cách • Chưa tấn công";
    }
}
