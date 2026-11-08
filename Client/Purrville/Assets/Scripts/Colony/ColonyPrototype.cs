using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Purrville.World;

namespace Purrville.Colony
{
    /// <summary>v0.3 colony loop: pathfinding, auto-harvest, stockpile, and house construction.</summary>
    public sealed class ColonyPrototype : MonoBehaviour
    {
        [SerializeField] private ProceduralWorld world;
        [SerializeField, Range(1, 20)] private int startingCats = 5;
        [SerializeField, Range(20, 500)] private int resourceCount = 180;
        [SerializeField] private float movementSpeed = 4f;
        [SerializeField] private int visionRadius = 7;
        [SerializeField] private bool automaticJobs = true;
        private readonly List<Cat> cats = new List<Cat>();
        private readonly List<Resource> resources = new List<Resource>();
        private readonly List<Vector2Int> vision = new List<Vector2Int>();
        private readonly List<Building> buildings = new List<Building>();
        private Sprite sprite;
        private Texture2D texture;
        private Camera cam;
        private bool initialized, buildMode;
        private int wood, stone, food;
        private float visionTimer, jobTimer, storyTimer = 20f;
        private ColonyStorySystem stories;
        private readonly List<CatProfile> profiles = new List<CatProfile>();
        private string latestStory = "Los primeros gatitos llegaron al pueblo.";
        private Vector2Int stockpile;
        private Cat selectedCat;
        private bool showCitizens = true, showHistory = true;
        private Vector2 citizensScroll, historyScroll;

        private sealed class Cat
        {
            public GameObject obj;
            public CatProfile profile;
            public Color color;
            public bool selected;
            public List<Vector2Int> path;
            public int pathIndex;
            public Resource target;
            public bool carrying;
            public Kind carriedKind;
            public Building construction;
            public float workTimer;
        }

        private enum Kind { Wood, Stone, Food }
        private sealed class Resource
        {
            public GameObject obj;
            public Kind kind;
            public int remaining = 6;
            public bool reserved;
            public Vector2Int cell;
        }

        private sealed class Building
        {
            public GameObject obj;
            public Vector2Int cell;
            public bool completed;
            public float progress;
            public bool reserved;
        }

        private void Start()
        {
            if (world == null) world = FindFirstObjectByType<ProceduralWorld>();
            cam = Camera.main;
        }

        private void Update()
        {
            if (world == null || !world.IsGenerated) return;
            if (!initialized) Initialize();
            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame) buildMode = !buildMode;
            if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame) automaticJobs = !automaticJobs;
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) showCitizens = !showCitizens;
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) showHistory = !showHistory;
            HandleMouse();
            storyTimer -= Time.deltaTime;
            if (stories != null && storyTimer <= 0f)
            {
                storyTimer = 25f;
                var story = stories.TryGenerate(profiles);
                if (story != null) { latestStory = story; Debug.Log("[Purrville Stories] " + story); }
            }
            jobTimer -= Time.deltaTime;
            if (automaticJobs && jobTimer <= 0)
            {
                jobTimer = .7f;
                foreach (var cat in cats) if (Idle(cat) && !cat.profile.NeedsRest) AssignJob(cat);
            }
            foreach (var cat in cats)
            {
                bool working = cat.target != null || cat.carrying || cat.construction != null ||
                    (cat.path != null && cat.pathIndex < cat.path.Count);
                cat.profile.Tick(Time.deltaTime, working);
                UpdateCat(cat);
            }
            visionTimer -= Time.deltaTime;
            if (visionTimer <= 0)
            {
                visionTimer = .3f;
                vision.Clear();
                foreach (var cat in cats) vision.Add(Cell(cat.obj.transform.position));
                world.RevealFromUnits(vision, visionRadius);
                foreach (var resource in resources)
                    if (resource.obj != null)
                        resource.obj.GetComponent<SpriteRenderer>().enabled =
                            world.IsDiscovered(resource.cell.x, resource.cell.y);
            }
        }

        private void Initialize()
        {
            initialized = true;
            texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0,0,1,1), new Vector2(.5f,.5f), 1f);
            int x = world.Width/2, y = world.Height/2;
            if (!FindLand(ref x, ref y)) { Debug.LogError("No valid starting land."); return; }
            stockpile = new Vector2Int(x,y);
            if (cam != null) cam.transform.position = new Vector3(x,y,cam.transform.position.z);
            MakeSprite("Stockpile",stockpile,new Color(.55f,.3f,.12f),1.2f,-.2f);
            var rng = new System.Random(world.Seed ^ 98231);
            stories = new ColonyStorySystem(world.Seed);
            for (int i=0; i<startingCats; i++)
            {
                int px=x+(i%3)-1, py=y+(i/3)+1;
                if (!Land(px,py)) {px=x;py=y;}
                var color = Color.HSVToRGB((i*.173f+.06f)%1f,.4f,.95f);
                var obj=MakeSprite("Cat_"+(i+1),new Vector2Int(px,py),color,.72f,-1f);
                var profile = CatProfile.Create(rng, i);
                obj.name = profile.Name;
                cats.Add(new Cat {obj=obj,color=color,profile=profile});
                profiles.Add(profile);
            }
            for (int i=0; i<resourceCount; i++)
            {
                int rx=rng.Next(world.Width), ry=rng.Next(world.Height);
                if (!Land(rx,ry) || (Mathf.Abs(rx-x)<3 && Mathf.Abs(ry-y)<3)) continue;
                var kind=(Kind)rng.Next(3);
                Color color=kind==Kind.Wood ? new Color(.13f,.35f,.13f) :
                    kind==Kind.Stone ? Color.gray : new Color(.9f,.3f,.4f);
                var cell=new Vector2Int(rx,ry);
                var obj=MakeSprite(kind+"_"+i,cell,color,.58f,-.5f);
                obj.GetComponent<SpriteRenderer>().enabled=false;
                resources.Add(new Resource {obj=obj,kind=kind,cell=cell});
            }
            Debug.Log("Purrville v0.3: colony initialized. B = house blueprint; J = auto jobs.");
        }

        private bool Land(int x,int y) =>
            world.TryGetTerrain(x,y,out var t) && t!=TerrainType.Water;

        private bool FindLand(ref int x,ref int y)
        {
            if (Land(x,y)) return true;
            for (int r=1;r<Mathf.Max(world.Width,world.Height);r++)
            for (int dy=-r;dy<=r;dy++)
            for (int dx=-r;dx<=r;dx++)
                if (Land(x+dx,y+dy)) {x+=dx;y+=dy;return true;}
            return false;
        }

        private GameObject MakeSprite(string name,Vector2Int cell,Color color,float scale,float z)
        {
            var obj=new GameObject(name);
            obj.transform.position=world.CellToWorld(cell.x,cell.y)+new Vector3(0,0,z);
            obj.transform.localScale=Vector3.one*scale;
            var renderer=obj.AddComponent<SpriteRenderer>();
            renderer.sprite=sprite;
            renderer.color=color;
            renderer.sortingOrder=2;
            return obj;
        }

        private Vector2Int Cell(Vector3 pos)
        {
            var local=world.transform.InverseTransformPoint(pos);
            return new Vector2Int(Mathf.RoundToInt(local.x),Mathf.RoundToInt(local.y));
        }

        private bool Move(Cat cat,Vector2Int target)
        {
            var path=GridPathfinder.Find(world,Cell(cat.obj.transform.position),target);
            if (path==null) return false;
            cat.path=path;
            cat.pathIndex=0;
            return true;
        }

        private bool Idle(Cat cat) => cat.target==null && !cat.carrying &&
            cat.construction==null && (cat.path==null || cat.pathIndex>=cat.path.Count);

        private void AssignJob(Cat cat)
        {
            foreach (var b in buildings)
                if (!b.completed && !b.reserved && wood>=12 && stone>=6)
                {
                    if (Move(cat,b.cell)) {cat.construction=b;b.reserved=true;return;}
                }
            Resource best=null;
            int distance=int.MaxValue;
            var current=Cell(cat.obj.transform.position);
            foreach (var r in resources)
            {
                if (r.reserved || r.obj==null || !world.IsDiscovered(r.cell.x,r.cell.y)) continue;
                int d=Mathf.Abs(current.x-r.cell.x)+Mathf.Abs(current.y-r.cell.y);
                if (cat.profile.Profession == CatProfession.Lumberjack && r.kind == Kind.Wood) d -= 12;
                if (cat.profile.Profession == CatProfession.Miner && r.kind == Kind.Stone) d -= 12;
                if (cat.profile.Profession == CatProfession.Forager && r.kind == Kind.Food) d -= 12;
                if (d<distance) {distance=d;best=r;}
            }
            if (best!=null && Move(cat,best.cell)) {cat.target=best;best.reserved=true;}
        }

        private void UpdateCat(Cat cat)
        {
            if (cat.profile.NeedsRest && cat.target==null && !cat.carrying && cat.construction==null)
            {
                cat.path = null;
                return;
            }
            if (cat.path!=null && cat.pathIndex<cat.path.Count)
            {
                Vector2Int next=cat.path[cat.pathIndex];
                Vector3 destination=world.CellToWorld(next.x,next.y)+new Vector3(0,0,-1);
                cat.obj.transform.position=Vector3.MoveTowards(cat.obj.transform.position,destination,
                    movementSpeed*Time.deltaTime);
                if (Vector2.Distance(cat.obj.transform.position,destination)<.05f) cat.pathIndex++;
                return;
            }
            if (cat.target!=null)
            {
                cat.workTimer+=Time.deltaTime;
                if (cat.workTimer<.8f) return;
                cat.workTimer=0;
                cat.carriedKind=cat.target.kind;
                cat.profile.JobsCompleted++;
                cat.carrying=true;
                cat.target.remaining--;
                cat.target.reserved=false;
                if (cat.target.remaining<=0)
                {
                    Destroy(cat.target.obj);
                    resources.Remove(cat.target);
                }
                cat.target=null;
                if (!Move(cat,stockpile)) cat.carrying=false;
                return;
            }
            if (cat.carrying)
            {
                switch (cat.carriedKind)
                {
                    case Kind.Wood: wood++;break;
                    case Kind.Stone: stone++;break;
                    case Kind.Food: food++;break;
                }
                cat.carrying=false;
            }
            if (cat.construction!=null)
            {
                var b=cat.construction;
                if (!b.completed && wood>=12 && stone>=6)
                {
                    b.progress+=Time.deltaTime;
                    if (b.progress>=3f)
                    {
                        wood-=12;stone-=6;
                        b.completed=true;
                        cat.profile.JobsCompleted++;
                        b.obj.GetComponent<SpriteRenderer>().color=new Color(.75f,.45f,.28f);
                        b.obj.name="House";
                        Debug.Log("Purrville: house completed!");
                    }
                }
                if (b.completed || wood<12 || stone<6)
                {
                    b.reserved=false;
                    cat.construction=null;
                }
            }
        }

        private void PlaceHouse(Vector2Int cell)
        {
            if (!Land(cell.x,cell.y) || !world.IsDiscovered(cell.x,cell.y)) return;
            foreach (var b in buildings) if (b.cell==cell) return;
            var obj=MakeSprite("HouseBlueprint",cell,new Color(.3f,.75f,1f,.7f),1.3f,-.3f);
            buildings.Add(new Building {obj=obj,cell=cell});
            buildMode=false;
        }

        private void HandleMouse()
        {
            if (cam==null || Mouse.current==null) return;
            Vector2 p=Mouse.current.position.ReadValue();
            Vector3 point=cam.ScreenToWorldPoint(new Vector3(p.x,p.y,-cam.transform.position.z));
            var cell=Cell(point);
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (buildMode) {PlaceHouse(cell);return;}
                Cat nearest=null;float best=.8f;
                foreach (var cat in cats)
                {
                    float d=Vector2.Distance(point,cat.obj.transform.position);
                    if (d<best) {best=d;nearest=cat;}
                }
                selectedCat = nearest;
                foreach (var cat in cats)
                {
                    cat.selected=cat==nearest;
                    cat.obj.GetComponent<SpriteRenderer>().color=cat.selected?Color.yellow:cat.color;
                }
            }
            if (!Mouse.current.rightButton.wasPressedThisFrame) return;
            Resource resource=null;float min=.85f;
            foreach (var r in resources)
            {
                if (r.obj==null || !world.IsDiscovered(r.cell.x,r.cell.y)) continue;
                float d=Vector2.Distance(point,r.obj.transform.position);
                if (d<min) {min=d;resource=r;}
            }
            foreach (var cat in cats)
            {
                if (!cat.selected) continue;
                if (cat.target!=null) cat.target.reserved=false;
                cat.target=null;
                if (cat.construction!=null) cat.construction.reserved=false;
                cat.construction=null;
                if (cat.carrying) continue;
                if (resource!=null && !resource.reserved && Move(cat,resource.cell))
                {
                    cat.target=resource;resource.reserved=true;
                }
                else Move(cat,cell);
            }
        }

        private void OnGUI()
        {
            if (!initialized) return;
            GUI.Box(new Rect(12,12,500,182),"Purrville — Colony Management v0.5");
            GUI.Label(new Rect(22,38,360,22),$"Wood: {wood}   Stone: {stone}   Food: {food}   Houses: {buildings.FindAll(b=>b.completed).Count}");
            GUI.Label(new Rect(22,62,360,22),$"Auto jobs: {(automaticJobs?"ON":"OFF")} (J)   |   House blueprint: B");
            GUI.Label(new Rect(22,85,390,22),buildMode?"CLICK on visible land to place a house":"Left click cat, right click to move/harvest");
            if (selectedCat != null)
            {
                var p = selectedCat.profile;
                GUI.Label(new Rect(22,108,475,24),$"{p.Name} | {p.Temperament} | {p.Profession} | Energy {p.Energy:0} | Mood {p.Mood:0}");
            }
            GUI.Label(new Rect(22,135,470,40),"Historia: " + latestStory);
            DrawManagementPanels();
        }

        private void DrawManagementPanels()
        {
            if (showCitizens)
            {
                float left = Mathf.Max(12, Screen.width - 335);
                GUI.Box(new Rect(left, 12, 323, 340), "Habitantes (TAB para ocultar)");
                GUILayout.BeginArea(new Rect(left + 10, 38, 303, 300));
                citizensScroll = GUILayout.BeginScrollView(citizensScroll);
                foreach (var cat in cats)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label(cat.profile.Name + " | " + cat.profile.Temperament);
                    GUILayout.Label("Energia " + cat.profile.Energy.ToString("0") +
                        " | Animo " + cat.profile.Mood.ToString("0") +
                        " | Tareas " + cat.profile.JobsCompleted);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Seleccionar", GUILayout.Width(92))) SelectCat(cat);
                    var options = (CatProfession[])Enum.GetValues(typeof(CatProfession));
                    if (GUILayout.Button("<", GUILayout.Width(30))) CycleProfession(cat.profile, options, -1);
                    GUILayout.Label(cat.profile.Profession.ToString(), GUILayout.Width(95));
                    if (GUILayout.Button(">", GUILayout.Width(30))) CycleProfession(cat.profile, options, 1);
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            if (showHistory)
            {
                float top = Mathf.Max(205, Screen.height - 205);
                GUI.Box(new Rect(12, top, 500, 193), "Cronica del pueblo (H para ocultar)");
                GUILayout.BeginArea(new Rect(22, top + 28, 480, 155));
                historyScroll = GUILayout.BeginScrollView(historyScroll);
                if (stories != null)
                    for (int i = stories.History.Count - 1; i >= 0; i--)
                        GUILayout.Label("- " + stories.History[i]);
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
        }

        private static void CycleProfession(CatProfile profile, CatProfession[] options, int direction)
        {
            int index = Array.IndexOf(options, profile.Profession);
            profile.Profession = options[(index + direction + options.Length) % options.Length];
        }

        private void SelectCat(Cat chosen)
        {
            selectedCat = chosen;
            foreach (var cat in cats)
            {
                cat.selected = cat == chosen;
                cat.obj.GetComponent<SpriteRenderer>().color = cat.selected ? Color.yellow : cat.color;
            }
            if (cam != null)
                cam.transform.position = new Vector3(chosen.obj.transform.position.x,
                    chosen.obj.transform.position.y, cam.transform.position.z);
        }

        private void OnDestroy()
        {
            if (sprite!=null) Destroy(sprite);
            if (texture!=null) Destroy(texture);
        }
    }
}
