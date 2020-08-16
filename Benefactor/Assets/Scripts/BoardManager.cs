﻿using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Linq;
using AStarSharp;

public class BoardManager : MonoBehaviour
{
    [Serializable]
    public class Count
    {
        public int minimum;
        public int maximum;

        public Count(int min, int max)
        {
            minimum = min;
            maximum = max;
        }
    }

    public int columns;
    public int rows;

    public Count wallCount = new Count(5, 9);
    public Count foodCount = new Count(1, 5);
    public Count houseCount = new Count(1, 4);
    public Count houseWallLength = new Count(5, 9);
    public GameObject player;
    public GameObject exit;
    public GameObject[] floorTiles;
    public GameObject[] wallTiles;
    public GameObject[] itemTiles;
    public GameObject[] enemyTiles;
    public GameObject[] outerWallTiles;
    public GameObject[] houseWallTiles;
    public GameObject keyDoor;

    private Transform boardHolder;
    private List<Vector3> gridPositions = new List<Vector3>();
    private List<List<Node>> Grid;

    void InitializeList()
    {
        gridPositions.Clear();
        for (int x = 1; x < columns - 1; x++)
        {
            for (int y = 1; y < rows - 1; y++)
            {
                gridPositions.Add(new Vector3(x, y, 0f));
            }
        }
    }


    private void BoardSetup()
    {
        boardHolder = new GameObject("Board").transform;
        Grid = new List<List<Node>>();
        for (int i = 0; i < columns; i++)
        {
            Grid.Add(new List<Node>());
            for (int j = 0; j < rows; j++)
            {
                Grid[i].Add(null);
            }
        }

        for (int x = -1; x < columns + 1; x++)
        {
            for (int y = -1; y < rows + 1; y++)
            {
                GameObject toInstantiate = floorTiles[Random.Range(0, floorTiles.Length)];
                if (x == -1 || x == columns || y == -1 || y == rows)
                {
                    toInstantiate = outerWallTiles[Random.Range(0, outerWallTiles.Length)];
                }
                else
                    Grid[x][y] = new Node(new Vector2(x, y), true, 1);

                GameObject instance = Instantiate(toInstantiate, new Vector3(x, y, 0f), Quaternion.identity) as GameObject;
                instance.transform.SetParent(boardHolder);
            }
        }
    }

    Vector3 RandomPosition()
    {
        int randomIndex = Random.Range(0, gridPositions.Count);
        Vector3 randomPosition = gridPositions[randomIndex];
        gridPositions.RemoveAt(randomIndex);
        return randomPosition;
    }

    void LayoutObjectAtRandom(GameObject[] tileArray, int minimum, int maximum)
    {
        int objectCount = Random.Range(minimum, maximum + 1);

        for (int i = 0; i < objectCount; i++)
        {
            Vector3 randomPosition = RandomPosition();
            GameObject tileChoice = tileArray[Random.Range(0, tileArray.Length)];
            Instantiate(tileChoice, randomPosition, Quaternion.identity);
            InteractableObject newObject = tileChoice.GetComponent<InteractableObject>();
            if (newObject != null)
            {
                Grid[(int)randomPosition.x][(int)randomPosition.y] = new Node(new Vector2((int)randomPosition.x, (int)randomPosition.y), newObject.damageable, (float)newObject.maxHealth + 1);
            }
        }
    }

    Vector3 RandomHousePosition(int length, int width, int door)
    {
        int randomIndex = Random.Range(0, gridPositions.Count);
        Vector3 randomPosition = gridPositions[randomIndex];

        for (int x = 0; x <= width; x++)
        {
            for (int y = 0; y <= length; y++)
            {
                Vector3 checkPosition = new Vector3(x + (int)randomPosition.x, y + (int)randomPosition.y, 0f);
                if (!gridPositions.Contains(checkPosition)) { return RandomHousePosition(length, width, door); }
            }
        }

        return randomPosition;
    }

    void LayoutHouses()
    {
        int houseCount = Random.Range(this.houseCount.minimum, this.houseCount.maximum + 1);

        for (int i = 0; i < houseCount; i++)
        {
            int length = Random.Range(houseWallLength.minimum, houseWallLength.maximum);
            int width = Random.Range(houseWallLength.minimum, houseWallLength.maximum);
            int door = Random.Range(1, width - 1);
            Vector3 randomPosition = RandomHousePosition(length, width, door);

            for (int x = 0; x <= width; x++)
            {
                for (int y = 0; y <= length; y++)
                {
                    Vector3 position = new Vector3(x + (int)randomPosition.x, y + (int)randomPosition.y, 0f);
                    gridPositions.Remove(position);

                    if (x == 0 || x == width || y == 0 || y == length)
                    {
                        GameObject tileChoice;
                        if (y == 0 && x == door)
                            tileChoice = keyDoor;
                        else
                            tileChoice = houseWallTiles[Random.Range(0, houseWallTiles.Length)];
                        Instantiate(tileChoice, position, Quaternion.identity);
                        InteractableObject newObject = tileChoice.GetComponent<InteractableObject>();
                        if (newObject != null)
                            Grid[(int)position.x][(int)position.y] = new Node(new Vector2((int)position.x, (int)position.y), newObject.damageable, (float)newObject.maxHealth + 1);
                    }
                }
            }

        }
    }

    public List<List<Node>> SetupScene(int level)
    {
        BoardSetup();
        InitializeList();
        LayoutHouses();
        LayoutObjectAtRandom(wallTiles, wallCount.minimum, wallCount.maximum);
        LayoutObjectAtRandom(itemTiles, foodCount.minimum, foodCount.maximum);
        int enemyCount = (int)Mathf.Log(level, 2f) + 1; //added 1
        LayoutObjectAtRandom(enemyTiles, enemyCount, enemyCount);
        Instantiate(exit, new Vector3(columns - 1, rows - 1, 0f), Quaternion.identity);
        //Instantiate(player, new Vector3(0, 0, 0f), Quaternion.identity);

        return Grid;
    }
}
