﻿using IListExtensions;
using Entitas;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CreateGameBoardSystem : IInitializeSystem, IReactiveSystem, ISetPool
{
    const int COLUMNS = 8;
    const int ROWS = 8;
    const int FOOD_COUNT_MIN = 1;
    const int FOOD_COUNT_MAX = 5;
    const int WALL_COUNT_MIN = 5;
    const int WALL_COUNT_MAX = 9;

    static readonly Prefab[] ENEMIES =
    {
        Prefab.Enemy1,
        Prefab.Enemy2,
    };
    static readonly Prefab[] FLOORS =
    {
        Prefab.Floor1,
        Prefab.Floor2,
        Prefab.Floor3,
        Prefab.Floor4,
        Prefab.Floor5,
        Prefab.Floor6,
        Prefab.Floor7,
        Prefab.Floor8,
    };
    static readonly Prefab[] FOOD =
    {
        Prefab.Food,
        Prefab.Soda,
    };
    static readonly Prefab[] OUTERWALLS =
    {
        Prefab.OuterWall1,
        Prefab.OuterWall2,
        Prefab.OuterWall3,
    };
    static readonly Prefab[] WALLS =
    {
        Prefab.Wall1,
        Prefab.Wall2,
        Prefab.Wall3,
        Prefab.Wall4,
        Prefab.Wall5,
        Prefab.Wall6,
        Prefab.Wall7,
        Prefab.Wall8,
    };
    static readonly Sprite[] DAMAGED_WALLS =
    {
        Sprite.Scavengers_SpriteSheet_48,
        Sprite.Scavengers_SpriteSheet_49,
        Sprite.Scavengers_SpriteSheet_50,
        Sprite.Scavengers_SpriteSheet_51,
        Sprite.Scavengers_SpriteSheet_52,
        Sprite.Scavengers_SpriteSheet_52, // used twice
        Sprite.Scavengers_SpriteSheet_53,
        Sprite.Scavengers_SpriteSheet_54,
    };

    Pool pool;
    Group deleteOnExitGroup;
    List<Vector2> gridPositions = new List<Vector2>();

    TriggerOnEvent IReactiveSystem.trigger
    {
        get { return Matcher.LevelTransitionDelay.OnEntityRemoved(); }
    }

    void ISetPool.SetPool(Pool pool)
    {
        this.pool = pool;
        deleteOnExitGroup = pool.GetGroup(Matcher.DeleteOnExit);
    }

    void IInitializeSystem.Initialize()
    {
        Debug.Log("Create GameBoard");

        pool.SetGameBoard(COLUMNS, ROWS);
        pool.SetLevel(1);
    }

    void IReactiveExecuteSystem.Execute(List<Entity> entities)
    {
        int level = pool.level.level;

        // delete previous elements
        foreach (var entity in deleteOnExitGroup.GetEntities())
        {
            pool.DestroyEntity(entity);
        }

        SetupScene(level);
    }

    void SetupScene(int level)
    {
        Debug.Log("Setup level " + level);

        //Creates the outer walls and floor.
        BoardSetup();

        InitialiseList();
        LayoutObjectAtRandom(WALLS, WALL_COUNT_MIN, WALL_COUNT_MAX, (e, i, ri) =>
        {
            e.AddDestructible(4)
             .AddDamageSprite(DAMAGED_WALLS[ri]);
        });
        LayoutObjectAtRandom(FOOD, FOOD_COUNT_MIN, FOOD_COUNT_MAX, (e, i, ri) =>
        {
            bool soda = e.resource.prefab == Prefab.Soda;
            int points = soda ? SharedConstants.SODA_POINTS : SharedConstants.FOOD_POINTS;
            var audio = soda ?
                new Audio[] { Audio.scavengers_soda1, Audio.scavengers_soda2 } :
                new Audio[] { Audio.scavengers_fruit1, Audio.scavengers_fruit2 };
            e.AddFood(points).AddAudioPickupSource(audio);
        });

        int enemyCount = (int)Mathf.Log(level, 2f);
        LayoutObjectAtRandom(ENEMIES, enemyCount, enemyCount, (e, i, ri) =>
        {
            bool enemy1 = e.resource.prefab == Prefab.Enemy1;
            int dmg = enemy1 ? SharedConstants.ENEMY1_DMG : SharedConstants.ENEMY2_DMG;

            // start at 1 because 0 is reserved for player
            e.AddTurnBased(i + 1, 0.1f)
             .IsAIMove(true)
             .AddFoodDamager(dmg)
             .AddSmoothMove(0.1f)
             .AddAudioAttackSource(Audio.scavengers_enemy1, Audio.scavengers_enemy2);
        });

        // Create exit
        pool.CreateEntity()
            .AddResource(Prefab.Exit)
            .IsExit(true)
            .IsGameBoardElement(true)
            .IsDeleteOnExit(true)
            .AddPosition(COLUMNS - 1, ROWS - 1);

        // Create player
        pool.CreateEntity()
            .AddResource(Prefab.Player)
            .IsGameBoardElement(true)
            .IsDeleteOnExit(true)
            .AddPosition(0, 0)
            .AddSmoothMove(0.1f)
            .IsControllable(true)
            .AddTurnBased(0, 0.1f)
            .IsActiveTurnBased(true)
            .IsAIMoveTarget(true)
            .AddAudioAttackSource(Audio.scavengers_chop1, Audio.scavengers_chop2)
            .AddAudioDeathSource(Audio.scavengers_die)
            .AddAudioWalkSource(Audio.scavengers_footstep1, Audio.scavengers_footstep2);
    }

    void BoardSetup()
    {
        // start at negative 1 to place outer edges
        for (int x = -1; x <= COLUMNS; x++)
        {
            for (int y = -1; y <= ROWS; y++)
            {
                bool edge = x == -1 || x == COLUMNS || y == -1 || y == ROWS;
                Prefab prefab = edge ? OUTERWALLS.Random() : FLOORS.Random();
                pool.CreateEntity().AddPosition(x, y)
                                   .AddResource(prefab)
                                   .IsDeleteOnExit(true)
                                   .AddNestedView("Board");
            }
        }
    }

    void InitialiseList()
    {
        gridPositions.Clear();

        // start at 1 to avoid placing items along the edges
        for (int x = 1; x < COLUMNS - 1; x++)
        {
            for (int y = 1; y < ROWS - 1; y++)
            {
                gridPositions.Add(new Vector2(x, y));
            }
        }
    }

    void LayoutObjectAtRandom(Prefab[] tileArray, int min, int max,
                              Action<Entity, int, int> postProcess)
    {
        int objectCount = UnityEngine.Random.Range(min, max + 1);
        for (int i = 0; i < objectCount; i++)
        {
            int randomIndex = gridPositions.RandomIndex();
            var randomPosition = gridPositions[randomIndex];
            //Remove the entry at randomIndex from the list so that it can't be re-used.
            gridPositions.RemoveAt(randomIndex);

            var randomTileIndex = tileArray.RandomIndex();
            var tileChoice = tileArray[randomTileIndex];
            var tile = pool.CreateEntity()
                           .IsGameBoardElement(true)
                           .IsDeleteOnExit(true)
                           .AddResource(tileChoice)
                           .AddPosition((int)randomPosition.x, (int)randomPosition.y);

            postProcess(tile, i, randomTileIndex);
        }
    }
}
