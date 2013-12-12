using UnityEngine;
using System.Collections.Generic;
using X_UniTMX;

public class MapLoader : MonoBehaviour {

	public TextAsset[] Maps;
	public int CurrentMap = 0;

	Map TiledMap;
	public string MapsPath = "Maps";

	Vector3 camPos = Vector3.zero;
	float ortographicSize;

	// Use this for initialization
	void Start () {
		LoadMap();
		camPos = Camera.main.transform.position;
		ortographicSize = Camera.main.orthographicSize;
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.LeftArrow))
		{
			CurrentMap--;
			if (CurrentMap < 0)
				CurrentMap = Maps.Length - 1;
			LoadMap();
		}
		if (Input.GetKeyDown(KeyCode.RightArrow))
		{
			CurrentMap++;
			if (CurrentMap > Maps.Length - 1)
				CurrentMap = 0;
			LoadMap();
		}

		if (Input.GetKey(KeyCode.W))
		{
			camPos.y += ortographicSize / 100;
		}
		if (Input.GetKey(KeyCode.S))
		{
			camPos.y -= ortographicSize / 100;
		}
		if (Input.GetKey(KeyCode.A))
		{
			camPos.x -= ortographicSize / 100;
		}
		if (Input.GetKey(KeyCode.D))
		{
			camPos.x += ortographicSize / 100;
		}
		Camera.main.transform.position = camPos;

	}

	void UnloadCurrentMap()
	{
		var children = new List<GameObject>();
		foreach (Transform child in this.transform) children.Add(child.gameObject);
		children.ForEach(child => Destroy(child));

		MeshFilter filter = GetComponent<MeshFilter>();
		if (filter)
			Destroy(filter);
	}

	void LoadMap()
	{
		UnloadCurrentMap();
		TiledMap = new Map(Maps[CurrentMap], true, MapsPath, this.gameObject);
		Debug.Log(TiledMap.ToString());
		MapObjectLayer mol = TiledMap.GetLayer("PropertyTest") as MapObjectLayer;
		if (mol != null)
		{
			Debug.Log(mol.GetPropertyAsBoolean("test"));
		}
	}
}
