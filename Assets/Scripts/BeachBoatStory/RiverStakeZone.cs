using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace BeachBoatStory
{
    /// <summary>Five work points build disjoint clusters of the authored underwater field.</summary>
    public sealed class RiverStakeZone : MonoBehaviour
    {
        [Serializable]
        public sealed class Placement
        {
            public Transform approach;
            public GameObject marker;
            public Transform stake;
            public int[] stakeIndices = new int[0];
            [NonSerialized] public bool planted;
            [NonSerialized] public Vector3 finalPosition;
            [NonSerialized] public TMP_Text label;
            [NonSerialized] public Vector3 stakeCenter;
        }

        public static RiverStakeZone Instance { get; private set; }
        [Min(1)] public float interactDistance = 24f;
        [Min(1)] public float plantingDuration = 3f;
        public Placement[] placements = new Placement[0];
        [Tooltip("All stakes in the field; saved inactive so they never appear before the quest.")]
        public Transform[] fieldStakes = new Transform[0];
        public int PlantedCount { get; private set; }
        public int RequiredCount => fieldStakes == null ? 0 : fieldStakes.Length;
        public int CompletedClusterCount { get; private set; }
        public int ClusterCount => placements == null ? 0 : placements.Length;
        public bool IsComplete => fieldRevealed && RequiredCount > 0 && PlantedCount == RequiredCount && CompletedClusterCount == ClusterCount;
        public bool IsPlanting { get; private set; }
        public bool RecoveryRequired { get; private set; }
        public Transform RecoveryTarget => lostStake != null && lostStake.activeSelf ? lostStake.transform : null;
        public bool HasBoatClearance(Vector3 position)
        {
            foreach (var center in centers)
                if (QuestRowboat.FlatDistance(position, center) < 50) return false;
            return true;
        }

        public int ExposedStakeCount
        {
            get
            {
                int count=0;
                foreach(var point in placements)
                    if(point!=null&&point.planted)foreach(int index in point.stakeIndices)
                        if(StakeReady(index)&&stakeTops[index]>TideSystem.CurrentWaterHeight+.25f)count++;
                return count;
            }
        }
        bool StakeReady(int i)=>i>=0&&i<fieldStakes.Length&&fieldStakes[i]!=null&&fieldStakes[i].gameObject.activeInHierarchy;
        /// <summary>Swept hull footprint against the actual planted stakes, not a timer or waypoint radius.</summary>
        public bool TryHullContact(Vector3 from,Vector3 to,Vector3 forward,float halfLength,float radius,bool exposedOnly,out Vector3 impact)
        {
            impact=default;
            from-=forward*halfLength;to+=forward*halfLength;from.y=to.y=0;
            var delta=to-from;
            foreach(var point in placements)
            {
                if(point==null||!point.planted)continue;
                foreach(int i in point.stakeIndices)
                {
                    if(!StakeReady(i)||(exposedOnly&&stakeTops[i]<=TideSystem.CurrentWaterHeight+.25f))continue;
                    var p=centers[i];p.y=0;
                    float t=delta.sqrMagnitude>.001f?Mathf.Clamp01(Vector3.Dot(p-from,delta)/delta.sqrMagnitude):0;
                    if((from+delta*t-p).sqrMagnitude>radius*radius)continue;
                    impact=new Vector3(p.x,Mathf.Max(TideSystem.CurrentWaterHeight,stakeTops[i]),p.z);return true;
                }
            }
            return false;
        }

        Vector3[] finalPositions, centers;
        float[] stakeTops;
        Collider[][] colliders;
        bool[][] colliderStates;
        bool[] finished;
        Coroutine planting;
        bool ready;
        Bounds fieldBounds;
        public Bounds FieldBounds=>fieldBounds;
        public bool ContainsHull(BoatHullCollision hull)
        {
            if(!ready||hull==null||hull.SolidHull==null)return false;
            var c=hull.SolidHull;
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var p=c.transform.TransformPoint(c.center+Vector3.Scale(c.size*.5f,new Vector3(x,0,z)));
                if(p.x<fieldBounds.min.x+8||p.x>fieldBounds.max.x-8||p.z<fieldBounds.min.z+8||p.z>fieldBounds.max.z-8)return false;
            }
            return true;
        }
        int workingIndex = -1;
        public int WorkingIndex=>workingIndex;
        bool fieldRevealed;
        Transform[] samplePosts;
        GameObject lostStake;
        bool recoveryTriggered;

        void Awake()
        {
            Instance = this;
            int count = RequiredCount;
            finalPositions = new Vector3[count]; centers = new Vector3[count];
            stakeTops = new float[count];
            colliders = new Collider[count][]; colliderStates = new bool[count][]; finished = new bool[count];
            var unique = new System.Collections.Generic.HashSet<Transform>();
            ready = count > 0;
            for (int i = 0; i < count; i++)
            {
                var stake = fieldStakes[i];
                if (stake == null || !unique.Add(stake)) { ready = false; continue; }
                finalPositions[i] = stake.position;
                var renderer = stake.GetComponentInChildren<Renderer>(true);
                centers[i] = renderer != null ? renderer.bounds.center : stake.position;
                stakeTops[i] = renderer != null ? renderer.bounds.max.y : stake.position.y;
                var extent=renderer!=null?renderer.bounds:new Bounds(stake.position,Vector3.one);
                if(i==0)fieldBounds=extent;else fieldBounds.Encapsulate(extent);
                colliders[i] = stake.GetComponentsInChildren<Collider>(true);
                colliderStates[i] = new bool[colliders[i].Length];
                for (int c = 0; c < colliders[i].Length; c++) colliderStates[i][c] = colliders[i][c].enabled;
                stake.gameObject.SetActive(false);
            }
            var assigned = new System.Collections.Generic.HashSet<int>();
            ready &= ClusterCount == 5;
            foreach (var point in placements ?? Array.Empty<Placement>())
            {
                if (point == null) { ready = false; continue; }
                if (point.approach == null || point.marker == null || point.stakeIndices == null || point.stakeIndices.Length == 0)
                    ready = false;
                foreach (int index in point.stakeIndices ?? Array.Empty<int>())
                    if (index < 0 || index >= count || !assigned.Add(index)) ready = false;
                point.planted = false;
                if (point.stake != null) point.finalPosition = point.stake.position;
                if (point.marker == null) continue;
                point.label = point.marker.GetComponentInChildren<TMP_Text>(true);
                if(point.label!=null)point.label.text=$"VỊ TRÍ {Array.IndexOf(placements,point)+1} • 1 CỌC MẪU";
                point.marker.SetActive(false);
            }
            ready &= assigned.Count == count;
            if (!ready) Debug.LogError("The river quest needs five nonempty clusters covering every field stake exactly once.", this);
            BuildLostStake();
        }

        void BuildLostStake()
        {
            lostStake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lostStake.name = "CocGoBiTroi_Recovery";
            lostStake.transform.SetParent(transform, true);
            lostStake.transform.localScale = new Vector3(2.2f, 16f, 2.2f);
            Destroy(lostStake.GetComponent<Collider>());
            Material material = null;
            foreach (var stake in fieldStakes)
            {
                if (stake == null) continue;
                var renderer = stake.GetComponentInChildren<Renderer>(true);
                if (renderer != null && renderer.sharedMaterial != null) { material = renderer.sharedMaterial; break; }
            }
            if (material != null) lostStake.GetComponent<Renderer>().sharedMaterial = material;
            lostStake.SetActive(false);
        }

        public int NearestUnplanted(Vector3 position)
        {
            if (!ready || IsComplete) return -1;
            int best = -1; float distance = float.PositiveInfinity;
            for (int i = 0; i < placements.Length; i++)
            {
                var p = placements[i];
                if (p == null || p.planted || p.approach == null) continue;
                float d = QuestRowboat.FlatDistance(position, p.approach.position);
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }

        public bool TryPlant(int index)
        {
            var story = BachDangStoryManager.Instance;
            var boat = QuestRowboat.Instance;
            if (!ready || !isActiveAndEnabled || BeachBoatQuestSession.InteractionBlocked || IsPlanting || IsComplete || RecoveryRequired ||
                story == null || story.currentState != BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver ||
                story.collectedWood < story.totalWoodRequired || index < 0 || index >= placements.Length ||
                boat == null || !boat.IsAboard || Mathf.Abs(boat.Speed) > 8) return false;
            var p = placements[index]; var player = story.GetPlayerTransform();
            if (p?.approach == null || p.planted || player == null || !player.gameObject.activeInHierarchy ||
                QuestRowboat.FlatDistance(boat.transform.position, p.approach.position) > interactDistance ||
                !HasBoatClearance(boat.transform.position)) return false;
            IsPlanting = true;
            CampaignProgress.Instance?.StartStakes();
            BeachBoatSoundscape.Instance?.PlayInteraction();
            workingIndex = index;
            var work=gameObject.AddComponent<StakeConstruction>();work.Begin(this);
            story.SetInteractPrompt(false, owner: this); story.UpdateUI();
            planting = StartCoroutine(PlantCluster(index));
            return true;
        }

        IEnumerator PlantCluster(int index)
        {
            while(StakeConstruction.Active!=null&&StakeConstruction.Active.Step!=StakeConstruction.WorkStep.Finished)yield return null;
            if(StakeConstruction.Active==null)yield break;
            var position=StakeConstruction.Active.FinishedStakePosition;var rotation=StakeConstruction.Active.FinishedStakeRotation;
            StakeConstruction.Active.FinishWork();planting=null;workingIndex=-1;
            var done=CaptureCheckpoint();done[index]=true;RestoreCheckpoint(done,false);SetSample(index,position,rotation);
            if(CompletedClusterCount<ClusterCount){BachDangStoryManager.Instance?.UpdateUI();yield break;}
            void CompleteField()
            {
                var all=new bool[placements.Length];for(int i=0;i<all.Length;i++)all[i]=true;
                RestoreCheckpoint(all);BachDangStoryManager.Instance?.OnPlantStakesComplete();
            }
            if(ScreenFader.Instance!=null)ScreenFader.Instance.FadeTransition("VÀI NGÀY SAU…\nTừ năm cọc mẫu tại năm vị trí, quân dân ngày đêm hoàn thành trận địa Bạch Đằng.",CompleteField,.7f,2.8f);
            else CompleteField();
        }

        void BeginRecoveryIncident()
        {
            recoveryTriggered = true; RecoveryRequired = true;
            var boat = QuestRowboat.Instance;
            Vector3 origin = boat != null ? boat.transform.position : transform.position;
            Vector3 forward = boat != null ? boat.transform.forward : Vector3.forward;
            Vector3 right = boat != null ? boat.transform.right : Vector3.right;
            var recoveryPosition=origin+forward*135f+right*70f;
            recoveryPosition.y=TideSystem.CurrentWaterHeight;
            // The expanded battle field must not place this quest item inside stakes or shallows.
            if(boat!=null&&(!HasBoatClearance(recoveryPosition)||!boat.CanOccupy(recoveryPosition,boat.transform.rotation)))
            {
                bool found=false;
                for(float radius=85;radius<=235&&!found;radius+=50)
                    for(int angle=0;angle<16;angle++)
                    {
                        var candidate=origin+Quaternion.Euler(0,angle*22.5f,0)*forward*radius;
                        candidate.y=TideSystem.CurrentWaterHeight;
                        if(!HasBoatClearance(candidate)||!boat.CanOccupy(candidate,boat.transform.rotation))continue;
                        recoveryPosition=candidate;found=true;break;
                    }
                if(!found)recoveryPosition=origin;
            }
            lostStake.transform.SetPositionAndRotation(recoveryPosition,
                Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(72f, 0, 0));
            var p = lostStake.transform.position; p.y = TideSystem.CurrentWaterHeight + 1.5f; lostStake.transform.position = p;
            lostStake.SetActive(true);
            BachDangStoryManager.Instance?.UpdateUI();
            StoryDialogueUI.Instance?.Show("Dân binh", "Một bó cọc vừa tuột dây và trôi theo dòng! Hãy chèo tới điểm đánh dấu, dừng thuyền rồi bấm E để vớt lại.");
        }

        public bool TryRecoverLostStake()
        {
            var boat = QuestRowboat.Instance;
            if (!RecoveryRequired || lostStake == null || boat == null || !boat.IsAboard ||
                BeachBoatQuestSession.InteractionBlocked || Mathf.Abs(boat.Speed) > 8f ||
                QuestRowboat.FlatDistance(boat.transform.position, lostStake.transform.position) > 30f) return false;
            RecoveryRequired = false; lostStake.SetActive(false);
            BeachBoatSoundscape.Instance?.PlayInteraction();
            BachDangStoryManager.Instance?.UpdateUI();
            StoryDialogueUI.Instance?.Show("Dân binh", "Đã vớt lại đủ cọc và siết chặt dây buộc. Tiếp tục hoàn thành hai cụm còn lại.");
            return true;
        }

        void RestoreColliders(int i)
        {
            if (colliders[i] == null) return;
            for (int c = 0; c < colliders[i].Length; c++)
                if (colliders[i][c] != null) colliders[i][c].enabled = colliderStates[i][c];
        }
        public bool[] CaptureCheckpoint()
        {
            var states=new bool[placements.Length];for(int i=0;i<states.Length;i++)states[i]=placements[i].planted;return states;
        }
        public void RestoreCheckpoint(bool[] states,bool reveal=true)
        {
            StakeConstruction.Active?.FinishWork();
            if(planting!=null)StopCoroutine(planting);planting=null;IsPlanting=false;workingIndex=-1;
            PlantedCount=CompletedClusterCount=0;RecoveryRequired=false;recoveryTriggered=false;
            fieldRevealed=reveal&&states!=null&&states.Length>=placements.Length;
            for(int i=0;i<placements.Length&&fieldRevealed;i++)fieldRevealed=states[i];
            if(samplePosts==null)samplePosts=new Transform[placements.Length];
            if(lostStake!=null)lostStake.SetActive(false);
            for(int i=0;i<fieldStakes.Length;i++)
            {finished[i]=false;if(fieldStakes[i]!=null){fieldStakes[i].position=finalPositions[i];fieldStakes[i].gameObject.SetActive(false);RestoreColliders(i);}}
            for(int p=0;p<placements.Length;p++)
            {
                bool planted=states!=null&&p<states.Length&&states[p];placements[p].planted=planted;
                if(placements[p].marker!=null)placements[p].marker.SetActive(false);
                if(samplePosts[p]!=null)samplePosts[p].gameObject.SetActive(planted&&!fieldRevealed);
                if(!planted)continue;CompletedClusterCount++;
                if(!fieldRevealed&&samplePosts[p]==null)SetSample(p,DefaultSamplePosition(p),Quaternion.Euler(0,0,-12));
                foreach(int i in placements[p].stakeIndices)if(i>=0&&i<fieldStakes.Length&&fieldStakes[i]!=null&&!finished[i])
                {finished[i]=true;fieldStakes[i].gameObject.SetActive(fieldRevealed);PlantedCount++;}
            }
            recoveryTriggered=CompletedClusterCount>=3;
        }

        Vector3 DefaultSamplePosition(int index)
        {var p=placements[index].approach.position+Vector3.right*30;p.y=TideSystem.CurrentWaterHeight+2;return p;}
        void SetSample(int index,Vector3 position,Quaternion rotation)
        {
            if(samplePosts[index]==null)
            {
                var post=StakeWorkVisual.Create("Coc_mau_vi_tri_"+index,transform,fieldStakes[0]);
                var solid=post.gameObject.AddComponent<CapsuleCollider>();solid.height=24;solid.radius=.8f;
                samplePosts[index]=post;
            }
            samplePosts[index].SetPositionAndRotation(position,rotation);
            samplePosts[index].gameObject.SetActive(!fieldRevealed&&placements[index].planted);
        }
        public Vector3[] CaptureSamplePositions()
        {var result=new Vector3[ClusterCount];for(int i=0;i<result.Length;i++)result[i]=samplePosts!=null&&samplePosts[i]!=null?samplePosts[i].position:DefaultSamplePosition(i);return result;}
        public Quaternion[] CaptureSampleRotations()
        {var result=new Quaternion[ClusterCount];for(int i=0;i<result.Length;i++)result[i]=samplePosts!=null&&samplePosts[i]!=null?samplePosts[i].rotation:Quaternion.Euler(0,0,-12);return result;}
        public void RestoreSamples(BachDangSaveData data)
        {
            for(int i=0;i<ClusterCount;i++)
                if(placements[i].planted&&!fieldRevealed)
                {
                    bool saved=data.stakeSamplesVersion>0&&data.stakeSamplePositions!=null&&i<data.stakeSamplePositions.Length&&data.stakeSampleRotations!=null&&i<data.stakeSampleRotations.Length;
                    SetSample(i,saved?data.stakeSamplePositions[i]:DefaultSamplePosition(i),saved?data.stakeSampleRotations[i]:Quaternion.Euler(0,0,-12));
                }
        }

        void Update()
        {
            var story = BachDangStoryManager.Instance;
            bool active = ready && !IsComplete && story != null &&
                story.currentState == BachDangStoryManager.StoryState.Act2_PlantStakesAtRiver &&
                !BeachBoatQuestSession.InteractionBlocked;
            var camera = BeachBoatQuestSession.Instance != null ? BeachBoatQuestSession.Instance.storyCamera : null;
            foreach (var p in placements)
            {
                if (p?.marker == null || p.approach == null) continue;
                var surface = p.approach.position; surface.y = TideSystem.CurrentWaterHeight + 1; p.approach.position = surface;
                bool visible = active && !RecoveryRequired && !p.planted;
                if (p.marker.activeSelf != visible) p.marker.SetActive(visible);
                if (visible && p.label != null && camera != null)
                    p.label.transform.rotation = Quaternion.LookRotation(p.label.transform.position - camera.transform.position);
            }
            if (!active) return;
            var boat = QuestRowboat.Instance;
            if (boat == null || !boat.IsAboard) return;
            if (RecoveryRequired && lostStake != null)
            {
                var recoveryPosition = lostStake.transform.position;
                recoveryPosition.y = TideSystem.CurrentWaterHeight + 1.5f + Mathf.Sin(Time.time * 1.8f) * .45f;
                lostStake.transform.position = recoveryPosition;
                float recoveryDistance = QuestRowboat.FlatDistance(boat.transform.position, recoveryPosition);
                if (recoveryDistance <= 30f)
                {
                    string recoveryPrompt = Mathf.Abs(boat.Speed) > 8f
                        ? "Thả W/S để dừng thuyền và vớt cọc bị trôi"
                        : "[E] Vớt bó cọc bị trôi và buộc lại dây";
                    story.SetInteractPrompt(true, recoveryPrompt, this);
                    if (Mathf.Abs(boat.Speed) <= 8f && !StoryDialogueUI.InputConsumedThisFrame &&
                        Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput()) TryRecoverLostStake();
                }
                return;
            }
            int index = NearestUnplanted(boat.transform.position);
            if (index < 0 || QuestRowboat.FlatDistance(boat.transform.position, placements[index].approach.position) > interactDistance) return;
            string prompt = Mathf.Abs(boat.Speed) > 8 ? "Thả W/S để dừng thuyền và đóng cọc" :
                !HasBoatClearance(boat.transform.position) ? "Lùi thuyền một chút để chừa chỗ dựng bãi cọc" :
                $"[E] Đóng cọc mẫu tại vị trí {index+1}/{ClusterCount} • Đã xong {CompletedClusterCount}/{ClusterCount}";
            story.SetInteractPrompt(true, prompt, this);
            if (!StoryDialogueUI.InputConsumedThisFrame && Input.GetKeyDown(KeyCode.E) && StoryDialogueUI.TryConsumeInput()) TryPlant(index);
        }

        void OnDisable()
        {
            StakeConstruction.Active?.FinishWork();
            if (planting != null) StopCoroutine(planting);
            if (IsPlanting && workingIndex >= 0)
            {
                foreach (int i in placements[workingIndex].stakeIndices)
                {
                    if (fieldStakes[i] == null) continue;
                    fieldStakes[i].position = finalPositions[i]; fieldStakes[i].gameObject.SetActive(false);
                    RestoreColliders(i);
                    if (finished[i]) PlantedCount--;
                    finished[i] = false;
                }
            }
            planting = null; IsPlanting = false; workingIndex = -1;
            foreach (var p in placements ?? Array.Empty<Placement>()) if (p?.marker != null) p.marker.SetActive(false);
            if (lostStake != null) lostStake.SetActive(false);
            if (BachDangStoryManager.Instance != null)
            { BachDangStoryManager.Instance.SetInteractPrompt(false, owner: this); BachDangStoryManager.Instance.UpdateUI(); }
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
