using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class GridManager : MonoBehaviour {
  private static int rows = 4;
  private static int cols = 4;
  private static int lowestTileValue = 2;
  private static float borderOffset = 0.05f;
  private static float horizontalOffset = -1.65f;
  private static float verticalOffset = 1.65f;
  private static float borderSpacing = 0.1f;
  private static float halfTileWidth = 0.55f;
  private static float spaceBetweenTiles = 1.1f;

  private int points;
  private List<GameObject> tiles;
  private Rect resetButton;
  private Rect gameOverButton;

  public int maxValue = 2048;
  public GameObject gameOverPanel;
  public GameObject noTile;
  public Text scoreText;
  public GameObject[] tilePrefabs;
  public LayerMask backgroundLayer;
  public float minSwipeDistance = 10.0f;

  private enum State {
    loaded,
    waitingForInput,
    checkingMatches,
    gameOver
  }

  private State state;

  #region monodevelop
  void Awake() {
    tiles = new List<GameObject>();
    state = State.loaded;
  }

  void Update() {
    if (state == State.gameOver) {
      gameOverPanel.SetActive(true);
    } else if (state == State.loaded) {
      state = State.waitingForInput;
	  // Load starting tiles
      generateRandomTile();
      generateRandomTile();
    } else if (state == State.waitingForInput) {
#if UNITY_STANDALONE
      if (Input.GetButtonDown("Left")) {
        if (moveTilesLeft()) {
          state = State.checkingMatches;
        }
      } else if (Input.GetButtonDown("Right")) {
        if (moveTilesRight()) {
          state = State.checkingMatches;
        }
      } else if (Input.GetButtonDown("Up")) {
        if (moveTilesUp()) {
          state = State.checkingMatches;
        }
      } else if (Input.GetButtonDown("Down")) {
        if (moveTilesDown()) {
          state = State.checkingMatches;
        }
      } else if (Input.GetButtonDown("reset")) {
        reset();
      } else if (Input.GetButtonDown("Quit")) {
        Application.Quit();
      }
#endif
    } else if (state == State.checkingMatches) {
      generateRandomTile();
      if (movesLeft()) {
        readyTilesForUpgrading();
        state = State.waitingForInput;
      } else {
        state = State.gameOver;
      }
    }
  }
  #endregion

  #region class methods
  private static Vector2 gridToWorld(int x, int y) {
    return new Vector2(x + horizontalOffset + borderSpacing * x,
                       -y + verticalOffset - borderSpacing * y);
  }

  private static Vector2 worldToGrid(float x, float y) {
    return new Vector2((x - horizontalOffset) / (1 + borderSpacing),
                       (y - verticalOffset) / -(1 + borderSpacing));
  }
  #endregion

  #region private methods
  private bool movesLeft() {
    if (tiles.Count < rows * cols) {
      return true;
    }

    for (int x = 0; x < cols; x++) {
      for (int y = 0; y < rows; y++) {
        Tile currentTile = getObjectAt(x, y).GetComponent<Tile>();
        Tile rightTile = getObjectAt(x + 1, y).GetComponent<Tile>();
        Tile downTile = getObjectAt(x, y + 1).GetComponent<Tile>();

        if (x != cols - 1 && currentTile.value == rightTile.value) {
          return true;
        } else if (y != rows - 1 && currentTile.value == downTile.value) {
          return true;
        }
      }
    }
    return false;
  }

  public void generateRandomTile() {
    if (tiles.Count >= rows * cols) {
      throw new UnityException("Unable to create new tile - grid is already full");
    }

    int value;
    value = lowestTileValue;

    // generate a random starting position
    int x = Random.Range(0, cols);
    int y = Random.Range(0, rows);

	// Loop through cells in grid for an empty position from the start point
    bool found = false;
    while (!found) {
      if (getObjectAt(x, y) == noTile) {
        found = true;
        Vector2 worldPosition = gridToWorld(x, y);
        GameObject obj;
        if (value == lowestTileValue) {
					obj = SimplePool.Spawn(tilePrefabs[0], worldPosition, transform.rotation);
        } else {
					obj = SimplePool.Spawn(tilePrefabs[1], worldPosition, transform.rotation);
        }

        tiles.Add(obj);
        TileAnimationHandler tileAnimManager = obj.GetComponent<TileAnimationHandler>();
        tileAnimManager.AnimateEntry();
      }

      x++;
      if (x >= cols) {
        y++;
        x = 0;
      }

      if (y >= rows) {
        y = 0;
      }
    }
  }

	private GameObject getObjectAt(int x, int y) {
    RaycastHit2D hit = Physics2D.Raycast(gridToWorld(x, y), Vector2.right, borderSpacing);

    if (hit && hit.collider.gameObject.GetComponent<Tile>() != null) {
      return hit.collider.gameObject;
    } else {
      return noTile;
    }
  }

  private bool moveTilesDown() {
    bool hasMoved = false;
    for (int y = rows - 1; y >= 0; y--) {
      for (int x = 0; x < cols; x++) {
        GameObject obj = getObjectAt(x, y);

        if (obj == noTile) {
          continue;
        }

        Vector2 raycastOrigin = obj.transform.position;
        raycastOrigin.y -= halfTileWidth;
        RaycastHit2D hit = Physics2D.Raycast(raycastOrigin, -Vector2.up, Mathf.Infinity);
        if (hit.collider != null) {
          GameObject hitObject = hit.collider.gameObject;
          if (hitObject != obj) {
            if (hitObject.tag == "Tile") {
              Tile thatTile = hitObject.GetComponent<Tile>();
              Tile thisTile = obj.GetComponent<Tile>();
              if (canUpgrade(thisTile, thatTile)) {
                upgradeTile(obj, thisTile, hitObject, thatTile);
                hasMoved = true;
              } else {
                Vector3 newPosition = hitObject.transform.position;
                newPosition.y += spaceBetweenTiles;
                if (!Mathf.Approximately(obj.transform.position.y, newPosition.y)) {
                  obj.transform.position = newPosition;
                  hasMoved = true;
                }
              }
            } else if (hitObject.tag == "Border") {
              Vector3 newPosition = obj.transform.position;
              newPosition.y = hit.point.y + halfTileWidth + borderOffset;
              if (!Mathf.Approximately(obj.transform.position.y, newPosition.y)) {
                obj.transform.position = newPosition;
                hasMoved = true;
              }
            }
          }
        }
      }
    }

    return hasMoved;
  }

  private bool moveTilesLeft() {
    bool hasMoved = false;
    for (int x = 1; x < cols; x++) {
      for (int y = 0; y < rows; y++) {
        GameObject obj = getObjectAt(x, y);

        if (obj == noTile) {
          continue;
        }

        Vector2 raycastOrigin = obj.transform.position;
        raycastOrigin.x -= halfTileWidth;
        RaycastHit2D hit = Physics2D.Raycast(raycastOrigin, -Vector2.right, Mathf.Infinity);
        if (hit.collider != null) {
          GameObject hitObject = hit.collider.gameObject;
          if (hitObject != obj) {
            if (hitObject.tag == "Tile") {
              Tile thatTile = hitObject.GetComponent<Tile>();
              Tile thisTile = obj.GetComponent<Tile>();
              if (canUpgrade(thisTile, thatTile)) {
                upgradeTile(obj, thisTile, hitObject, thatTile);
                hasMoved = true;
              } else {
                Vector3 newPosition = hitObject.transform.position;
                newPosition.x += spaceBetweenTiles;
                if (!Mathf.Approximately(obj.transform.position.x, newPosition.x)) {
                  obj.transform.position = newPosition;
                  hasMoved = true;
                }
              }
            } else if (hitObject.tag == "Border") {
              Vector3 newPosition = obj.transform.position;
              newPosition.x = hit.point.x + halfTileWidth + borderOffset;
              if (!Mathf.Approximately(obj.transform.position.x, newPosition.x)) {
                obj.transform.position = newPosition;
                hasMoved = true;
              }
            }
          }
        }
      }
    }

    return hasMoved;
  }

  private bool moveTilesRight() {
    bool hasMoved = false;
    for (int x = cols - 1; x >= 0; x--) {
      for (int y = 0; y < rows; y++) {
        GameObject obj = getObjectAt(x, y);

        if (obj == noTile) {
          continue;
        }

        Vector2 raycastOrigin = obj.transform.position;
        raycastOrigin.x += halfTileWidth;
        RaycastHit2D hit = Physics2D.Raycast(raycastOrigin, Vector2.right, Mathf.Infinity);
        if (hit.collider != null) {
          GameObject hitObject = hit.collider.gameObject;
          if (hitObject != obj) {
            if (hitObject.tag == "Tile") {
              Tile thatTile = hitObject.GetComponent<Tile>();
              Tile thisTile = obj.GetComponent<Tile>();
              if (canUpgrade(thisTile, thatTile)) {
                upgradeTile(obj, thisTile, hitObject, thatTile);
                hasMoved = true;
              } else {
                Vector3 newPosition = hitObject.transform.position;
                newPosition.x -= spaceBetweenTiles;
                if (!Mathf.Approximately(obj.transform.position.x, newPosition.x)) {
                  obj.transform.position = newPosition;
                  hasMoved = true;
                }
              }
            } else if (hitObject.tag == "Border") {
              Vector3 newPosition = obj.transform.position;
              newPosition.x = hit.point.x - halfTileWidth - borderOffset;
              if (!Mathf.Approximately(obj.transform.position.x, newPosition.x)) {
                obj.transform.position = newPosition;
                hasMoved = true;
              }
            }
          }
        }
      }
    }

    return hasMoved;
  }

  private bool moveTilesUp() {
    bool hasMoved = false;
    for (int y = 1; y < rows; y++) {
      for (int x = 0; x < cols; x++) {
        GameObject obj = getObjectAt(x, y);

        if (obj == noTile) {
          continue;
        }

        Vector2 raycastOrigin = obj.transform.position;
        raycastOrigin.y += halfTileWidth;
        RaycastHit2D hit = Physics2D.Raycast(raycastOrigin, Vector2.up, Mathf.Infinity);
        if (hit.collider != null) {
          GameObject hitObject = hit.collider.gameObject;
          if (hitObject != obj) {
            if (hitObject.tag == "Tile") {
              Tile thatTile = hitObject.GetComponent<Tile>();
              Tile thisTile = obj.GetComponent<Tile>();
              if (canUpgrade(thisTile, thatTile)) {
                upgradeTile(obj, thisTile, hitObject, thatTile);
                hasMoved = true;
              } else {
                Vector3 newPosition = hitObject.transform.position;
                newPosition.y -= spaceBetweenTiles;
                if (!Mathf.Approximately(obj.transform.position.y, newPosition.y)) {
                  obj.transform.position = newPosition;
                  hasMoved = true;
                }
              }
            } else if (hitObject.tag == "Border") {
              Vector3 newPosition = obj.transform.position;
              newPosition.y = hit.point.y - halfTileWidth - borderOffset;
              if (!Mathf.Approximately(obj.transform.position.y, newPosition.y)) {
                obj.transform.position = newPosition;
                hasMoved = true;
              }
            }
          }
        }
      }
    }

    return hasMoved;
  }

  private bool canUpgrade(Tile thisTile, Tile thatTile) {
    return (thisTile.value != maxValue && thisTile.power == thatTile.power && !thisTile.upgradedThisTurn && !thatTile.upgradedThisTurn);
  }

  private void readyTilesForUpgrading() {
    foreach (var obj in tiles) {
      Tile tile = obj.GetComponent<Tile>();
      tile.upgradedThisTurn = false;
    }
  }

  public void reset() {
    gameOverPanel.SetActive(false);
    foreach (var tile in tiles) {
			SimplePool.Despawn(tile);
    }

    tiles.Clear();
    points = 0;
    scoreText.text = "0";
    state = State.loaded;
  }

  private void upgradeTile(GameObject toDestroy, Tile destroyTile, GameObject toUpgrade, Tile upgradeTile) {
    Vector3 toUpgradePosition = toUpgrade.transform.position;

    tiles.Remove(toDestroy);
    tiles.Remove(toUpgrade);

		SimplePool.Despawn(toDestroy);
		SimplePool.Despawn(toUpgrade);

    // create the upgraded tile
		GameObject newTile = SimplePool.Spawn(tilePrefabs[upgradeTile.power], toUpgradePosition, transform.rotation);
    tiles.Add(newTile);
    Tile tile = newTile.GetComponent<Tile>();
    tile.upgradedThisTurn = true;

    points += upgradeTile.value * 2;
    scoreText.text = points.ToString();

    TileAnimationHandler tileAnim = newTile.GetComponent<TileAnimationHandler>();
    tileAnim.AnimateUpgrade();
  }
  #endregion
}
