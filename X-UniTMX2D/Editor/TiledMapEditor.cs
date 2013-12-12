/*!
 * X-UniTMX: A tiled map editor file importer for Unity3d
 * https://bitbucket.org/Chaoseiro/x-unitmx
 * 
 * Copyright 2013 Guilherme "Chaoseiro" Maia
 * Released under the MIT license
 * Check LICENSE.MIT for more details.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using X_UniTMX;
using UnityEditor;
using UnityEngine;

namespace X_UniTMX
{
	[CustomEditor (typeof(TiledMapComponent))]
	public class TiledMapEditor : Editor
	{	
		int arraySize = 0;
		List<string> collidersLayers;
		List<float> collidersWidth;
		List<float> collidersZDepth;
		List<bool> collidersIsInner;
		bool foldout = false;

		void OnEnable()
		{
			collidersLayers = new List<string>();
			//collidersWidth = new List<float>();
			//collidersZDepth = new List<float>();
			//collidersIsInner = new List<bool>();
		}

		public override void OnInspectorGUI()
		{
			//base.OnInspectorGUI();
			//DrawDefaultInspector();
			serializedObject.Update();

			EditorGUIUtility.LookLikeInspector();

			TiledMapComponent TMEditor = (TiledMapComponent)target;

			TMEditor.MapTMX = (TextAsset)EditorGUILayout.ObjectField("Tiled Map", TMEditor.MapTMX, typeof(TextAsset));

			TMEditor.GenerateCollider = EditorGUILayout.BeginToggleGroup("Generate Colliders", TMEditor.GenerateCollider);
			//TMEditor.CollidersZDepth = EditorGUILayout.FloatField("Colliders Z Depth", TMEditor.CollidersZDepth);
			//TMEditor.CollidersWidth = EditorGUILayout.FloatField("Colliders Width", TMEditor.CollidersWidth);

			foldout = EditorGUILayout.Foldout(foldout, "Colliders Layers");
			if (foldout)
			{
				if (TMEditor.CollidersLayerName != null && TMEditor.CollidersLayerName.Length > 0)
					arraySize = TMEditor.CollidersLayerName.Length;

				arraySize = EditorGUILayout.IntField("Colliders Layers Number", arraySize);
				
				int i = 0;
				if (collidersLayers.Count < arraySize)
				{
					while (collidersLayers.Count < arraySize)
					{
						collidersLayers.Add("Collider_" + i);
						i++;
					}
					TMEditor.CollidersLayerName = new string[arraySize];
					TMEditor.CollidersWidth = new float[arraySize];
					TMEditor.CollidersZDepth = new float[arraySize];
					TMEditor.CollidersIsInner = new bool[arraySize];
				}
				else if (collidersLayers.Count > arraySize)
				{
					while (collidersLayers.Count > arraySize)
					{
						collidersLayers.RemoveAt(collidersLayers.Count - 1);
					}
					TMEditor.CollidersLayerName = new string[arraySize];
					TMEditor.CollidersWidth = new float[arraySize];
					TMEditor.CollidersZDepth = new float[arraySize];
					TMEditor.CollidersIsInner = new bool[arraySize];
				}
				
				for (i = 0; i < arraySize; i++)
				{
					collidersLayers[i] = EditorGUILayout.TextField("Collider Layer "+i, collidersLayers[i]);
					TMEditor.CollidersWidth[i] = EditorGUILayout.FloatField("Collider " + i + " Width", TMEditor.CollidersWidth[i]);
					TMEditor.CollidersZDepth[i] = EditorGUILayout.FloatField("Collider " + i + " Z Depth", TMEditor.CollidersZDepth[i]);
					TMEditor.CollidersIsInner[i] = EditorGUILayout.Toggle("Collider " + i + " Is Inner Collisions", TMEditor.CollidersIsInner[i]);
					TMEditor.CollidersLayerName[i] = collidersLayers[i];
				}
			}
			EditorGUILayout.EndToggleGroup();
			
			if (GUILayout.Button("Import Tile Map"))
			{
				// Destroy any previous map entities
				var children = new List<GameObject>();
				foreach (Transform child in TMEditor.transform) children.Add(child.gameObject);
				children.ForEach(child => DestroyImmediate(child, true));

				MeshFilter filter = TMEditor.GetComponent<MeshFilter>();
				if (filter)
					DestroyImmediate(filter, true);
				string fullPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(TMEditor.MapTMX));
				string mapPath = "";
				string[] splittedFullPath = fullPath.Split(new string[] { "Assets/Resources/" }, StringSplitOptions.None);
				if(splittedFullPath.Length > 1)
					mapPath = splittedFullPath[1] + "/";
				//if (mapPath.LastIndexOf('/') == -1) // means it only returned a filename
				//	mapPath = ""; // so set the path to an empty string
				//else
				//	mapPath = mapPath.Remove(mapPath.LastIndexOf('/')); // otherwise eleminate the filename and store the relative path
				TMEditor.Initialize(fullPath, mapPath);

				if (TMEditor.GenerateCollider)
				{
					TMEditor.GenerateColliders();
				}

			}
		}//*/
	}
}
