using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Purrville.World;

namespace Purrville.Colony
{
    /// <summary>Playable prototype: cats, selection, right-click orders, resource harvesting and fog reveal.</summary>
    public sealed class ColonyPrototype : MonoBehaviour
    {
        [SerializeField] private ProceduralWorld world;
        [SerializeField] private int startingCats = 5;
        [SerializeField] private int resourceCount = 120;
        [SerializeField] private float movementSpeed = 4f;
        [SerializeField] private int visionRadius = 7;
        private readonly List<Cat> cats = new List<Cat>();
        private readonly List<Resource> resources = new List<Resource>();
        private readonly List<Vector2Int> positions = new List<Vector2Int>();
        private Sprite sprite;
        private int wood, stone, food;
        private float visionTimer;
        private bool initialized;
        private Camera cam;

        private sealed class Cat
        {
            public GameObject obj;
            public Vector3 destination;
            public Resource target;
            public bool selected;
        }

        private enum Kind { Wood, Stone, Food }
        private sealed class Resource
        {
            public GameObject obj;
            public Kind kind;
            public int remaining = 5;
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
            HandleMouse();
            foreach (var cat in cats)
            {
                if (cat.target != null && cat.target.obj == null) cat.target = null;
                Vector3 destination = cat.target != null ? cat.target.obj.transform.position : cat.destination;
                if (Vector3.Distance(cat.obj.transform.position, destination) > .8f)
                    cat.obj.transform.position = Vector3.MoveTowards(cat.obj.transform.position, destination, movementSpeed * Time.deltaTime);
                else if (cat.target != null)
                {
                    cat.target.remaining--;
                    switch (cat.target.kind)
                    {
                        case Kind.Wood: wood++; break;
                        case Kind.Stone: stone++; break;
                        case Kind.Food: food++; break;
                    }
                    if (cat.target.remaining <= 0)
                    {
                        Destroy(cat.target.obj);
                        resources.Remove(cat.target);
                        cat.target = null;
                    }
                    else cat.target = null; // one resource per order
                }
            }
            visionTimer -= Time.deltaTime;
            if (visionTimer <= 0)
            {
                visionTimer = .25f;
                positions.Clear();
                foreach (var cat in cats)
                {
                    Vector3 p = world.transform.InverseTransformPoint(cat.obj.transform.position);
                    positions.Add(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y)));
                }
                world.RevealFromUnits(positions, visionRadius);
            }
        }

        private void Initialize()
        {
            initialized = true;
            var rng = new System.Random(world.Seed ^ 98231);
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0,0,1,1), new Vector2(.5f,.5f), 1f);
            int cx = world.Width / 2, cy = world.Height / 2;
            if (!FindLand(ref cx, ref cy)) { Debug.LogWarning("No land for colony"); return; }
            for (int i = 0; i < startingCats; i++)
            {
                int x = cx + (i % 3) - 1, y = cy + i / 3;
                if (!IsLand(x,y)) { x=cx; y=cy; }
                var obj = MakeSprite("Cat_" + (i+1), x, y, new Color(.95f - i*.1f,.64f + i*.06f,.36f + i*.07f), .72f, -1);
                cats.Add(new Cat { obj=obj, destination=obj.transform.position });
            }
            for (int i = 0; i < resourceCount; i++)
            {
                int x=rng.Next(world.Width), y=rng.Next(world.Height);
                if (!IsLand(x,y)) continue;
                Kind kind = (Kind)rng.Next(3);
                Color color = kind == Kind.Wood ? new Color(.13f,.35f,.13f) :
                    kind == Kind.Stone ? Color.gray : new Color(.9f,.3f,.4f);
                var obj=MakeSprite(kind+"_"+i,x,y,color,.58f,-.5f);
                resources.Add(new Resource {obj=obj,kind=kind});
            }
            Debug.Log("Purrville: cats and harvestable resources spawned.");
        }

        private bool IsLand(int x,int y)
        {
            return world.TryGetTerrain(x,y,out var type) && type != TerrainType.Water;
        }

        private bool FindLand(ref int x,ref int y)
        {
            if (IsLand(x,y)) return true;
            for (int radius=1;radius<Mathf.Max(world.Width,world.Height);radius++)
            for (int dy=-radius;dy<=radius;dy++)
            for (int dx=-radius;dx<=radius;dx++)
                if (IsLand(x+dx,y+dy)) {x+=dx;y+=dy;return true;}
            return false;
        }

        private GameObject MakeSprite(string name,int x,int y,Color color,float scale,float z)
        {
            var obj=new GameObject(name);
            obj.transform.position=world.CellToWorld(x,y)+new Vector3(0,0,z);
            obj.transform.localScale=Vector3.one*scale;
            var renderer=obj.AddComponent<SpriteRenderer>();
            renderer.sprite=sprite;
            renderer.color=color;
            renderer.sortingOrder=2;
            return obj;
        }

        private void HandleMouse()
        {
            if (cam==null || Mouse.current==null) return;
            Vector2 pointer=Mouse.current.position.ReadValue();
            Vector3 mouse=cam.ScreenToWorldPoint(new Vector3(pointer.x,pointer.y, -cam.transform.position.z));
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cat nearest=null; float best=.8f;
                foreach (var cat in cats)
                {
                    float d=Vector2.Distance(mouse,cat.obj.transform.position);
                    if (d<best) {best=d;nearest=cat;}
                }
                foreach (var cat in cats)
                {
                    cat.selected=cat==nearest;
                    cat.obj.GetComponent<SpriteRenderer>().color=cat.selected ? Color.yellow :
                        new Color(.95f - cats.IndexOf(cat)*.1f,.64f + cats.IndexOf(cat)*.06f,.36f + cats.IndexOf(cat)*.07f);
                }
            }
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                Resource nearest=null; float best=.85f;
                foreach (var resource in resources)
                {
                    if (resource.obj==null) continue;
                    float d=Vector2.Distance(mouse,resource.obj.transform.position);
                    if (d<best) {best=d;nearest=resource;}
                }
                foreach (var cat in cats)
                    if (cat.selected)
                    {
                        cat.target=nearest;
                        cat.destination=new Vector3(mouse.x,mouse.y,cat.obj.transform.position.z);
                    }
            }
        }

        private void OnGUI()
        {
            if (!initialized) return;
            GUI.Box(new Rect(12,12,285,88),"Purrville — Colony Prototype");
            GUI.Label(new Rect(22,38,270,22),$"Wood: {wood}   Stone: {stone}   Food: {food}");
            GUI.Label(new Rect(22,62,270,22),"Left click: cat  |  Right click: order");
        }
    }
}
