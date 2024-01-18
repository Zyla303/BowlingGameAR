
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class BowlingAlleyPlacer : MonoBehaviour
{
    [Header( "AR Components" )]
    [SerializeField]
    private ARRaycastManager arRaycastManager;
    [SerializeField]
    private ARSessionOrigin arSessionOrigin;
    [SerializeField]
    private ARPlaneManager arPlaneManager;

    [Header( "Game Objects" )]
    [SerializeField]
    private GameObject bowlingAlleyPrefab;
    [SerializeField]
    private GameObject bowlingBallPrefab;
    [SerializeField]
    private List<GameObject> pins; // Lista obiekt�w kr�gli

    [Header( "UI Components" )]
    [SerializeField]
    private Button startButton;
    [SerializeField]
    private Button resetButton;
    [SerializeField]
    private Text placementText;
    [SerializeField]
    private Text scoreText;

    [Header( "Alley Scale Settings" )]
    [SerializeField]
    private float minimumAlleyLength = 3.0f; // Minimalna d�ugo�� toru
    [SerializeField]
    private float maximumAlleyLength = 8.0f; // Maksymalna d�ugo�� toru

    private GameObject bowlingSetup;
    private GameObject spawnedBall;
    private Camera arCamera;
    private int score = 0, throwCounter = 0;
    private bool isBallThrown = false;
    private bool pinsInitialized = false;
    private List<PinData> originalPinData = new List<PinData>();
    private HashSet<GameObject> countedPins;
    private bool wasGameReset = false;

    void Start()
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        countedPins = new HashSet<GameObject>();
        arCamera = arSessionOrigin.GetComponentInChildren<Camera>();
        bowlingAlleyPrefab.SetActive( false );
        bowlingBallPrefab.SetActive( false );

        foreach( GameObject pin in pins )
        {
            pin.SetActive( false ); // Ukrycie kr�gli na pocz�tku
            PinCollision pinCollision = pin.AddComponent<PinCollision>();
            pinCollision.alleyPlacer = this;
        }
        resetButton.gameObject.SetActive( false );
        placementText.text = "Tap to place bowling alley";
        startButton.onClick.AddListener( OnStartButtonPressed );
        resetButton.onClick.AddListener( ResetBallAndPins );
    }

    void Update()
    {
        if( bowlingSetup == null && Input.touchCount > 0 )
        {
            Touch touch = Input.GetTouch( 0 );
            if( touch.phase == TouchPhase.Began )
            {
                List<ARRaycastHit> hits = new List<ARRaycastHit>();
                if( arRaycastManager.Raycast( touch.position, hits, TrackableType.PlaneWithinPolygon ) )
                {
                    Pose hitPose = hits[0].pose;

                    // Pobieranie wielko�ci wybranej powierzchni
                    ARPlane hitPlane = arPlaneManager.GetPlane( hits[0].trackableId );
                    Vector2 planeSize = new Vector2( hitPlane.extents.x * 2, hitPlane.extents.y * 2 );

                    PlaceBowlingAlleyAndPins( hitPose.position, hitPose.rotation, planeSize );
                    HideDetectedPlanes();
                }
            }
        }
        if( spawnedBall != null && spawnedBall.transform.position.y < -2f )
        {
            StartCoroutine( ResetBallAfterDelay( 1f ) ); // Resetowanie pi�ki po 1 sekundzie, je�li jest poni�ej -2f
        }

        if( isBallThrown && HasBallStopped( spawnedBall ) )
        {
            ResetBallPosition(); // Resetowanie pozycji pi�ki, je�li si� zatrzyma�a
            isBallThrown = false;
        }

        foreach( GameObject pin in pins )
        {
            if( !countedPins.Contains( pin ) && IsPinKnockedOver( pin ) )
            {
                AddScore( pin );
            }
        }

    }

    private void HideDetectedPlanes()
    {
        foreach( var plane in arPlaneManager.trackables )
        {
            plane.gameObject.SetActive( false );
        }
        arPlaneManager.enabled = false; // Opcjonalnie, wy��cz AR Plane Manager
    }

    private void PlaceBowlingAlleyAndPins( Vector3 position, Quaternion rotation, Vector2 planeSize )
    {
        placementText.gameObject.SetActive( false );
        bowlingSetup = new GameObject( "Bowling Setup" );
        bowlingSetup.transform.position = position;
        bowlingSetup.transform.rotation = rotation;

        GameObject alleyInstance = Instantiate( bowlingAlleyPrefab, position, rotation, bowlingSetup.transform );
        alleyInstance.SetActive( true );

        // Skalowanie toru na podstawie wielko�ci wybranej powierzchni
        ScaleBowlingAlley( alleyInstance, planeSize );

        // Dostosowanie pozycji toru do kr�gli po skalowaniu
        Vector3 alleyPositionAdjustment = rotation * new Vector3( 0, -1.0f, alleyInstance.GetComponent<MeshRenderer>().bounds.extents.z );
        Vector3 alleyPosition = position + alleyPositionAdjustment;
        alleyInstance.transform.position = alleyPosition;

        // Pobranie d�ugo�ci toru z instancji toru
        MeshRenderer alleyMeshRenderer = alleyInstance.GetComponent<MeshRenderer>();
        float torLength = alleyMeshRenderer.bounds.size.z;

        Vector3[] pinPositions = new Vector3[10]
        {
            new Vector3(0, 0.255f, (torLength / 2) - 1.25f),
            new Vector3(0.133333f, 0.255f, (torLength / 2) - 1.00f),
            new Vector3(-0.133333f, 0.255f, (torLength / 2) - 1.00f),
            new Vector3(0.2666667f, 0.255f, (torLength / 2) - 0.75f),
            new Vector3(0, 0.255f, (torLength / 2) - 0.75f),
            new Vector3(-0.2666667f, 0.255f, (torLength / 2) - 0.75f),
            new Vector3(0.4f, 0.255f, (torLength / 2) - 0.5f),
            new Vector3(0.133333f, 0.255f, (torLength / 2) - 0.5f),
            new Vector3(-0.133333f, 0.255f, (torLength / 2) - 0.5f),
            new Vector3(-0.4f, 0.255f, (torLength / 2) - 0.5f)
        };


        for( int i = 0; i < pins.Count; i++ )
        {
            GameObject pin = pins[i];
            Vector3 pinPosition = pinPositions[i];
            pin.transform.position = alleyPosition + ( rotation * pinPosition );
            pin.transform.rotation = Quaternion.Euler( -90, 0, 0 ); // Upright rotation
            if( !pinsInitialized )
            {
                // Zapisanie pocz�tkowej pozycji i rotacji kr�gli
                originalPinData.Add( new PinData( pin.transform.position, pin.transform.rotation ) );
            }

            pin.SetActive( true );
        }

        pinsInitialized = true;

        startButton.gameObject.SetActive( true );
        resetButton.gameObject.SetActive( true );

    }

    private void ScaleBowlingAlley( GameObject alleyInstance, Vector2 planeSize )
    {
        // Obliczanie docelowej d�ugo�ci toru jako 1.5 razy d�ugo�� wykrytej powierzchni
        float desiredLength = 0.0f;
        if( planeSize.y >= planeSize.x)
            desiredLength = planeSize.y;
        else
        {
            desiredLength = planeSize.x;
        }

        if( desiredLength <= 2f )
        {
            desiredLength = desiredLength * 4f;

        }else if(desiredLength < 2f && desiredLength < 4f )
        {
            desiredLength = desiredLength * 2f;
        }

        // Obliczenie skali toru
        MeshRenderer alleyMeshRenderer = alleyInstance.GetComponent<MeshRenderer>();
        float originalLength = alleyMeshRenderer.bounds.size.z;
        float scaleRatioZ = desiredLength / originalLength;

        // Ograniczenie skali, aby tor nie by� zbyt ma�y ani zbyt du�y
        scaleRatioZ = Mathf.Clamp( scaleRatioZ, minimumAlleyLength / originalLength, maximumAlleyLength / originalLength );

        // Skalowanie tylko d�ugo�ci toru (sk�adnik z), zachowanie oryginalnej szeroko�ci (x) i wysoko�ci (y)
        Vector3 currentScale = alleyInstance.transform.localScale;
        alleyInstance.transform.localScale = new Vector3( currentScale.x, currentScale.y, scaleRatioZ );
    }


    private bool AreAllPinsKnockedOver()
    {
        foreach( GameObject pin in pins )
        {
            if( pin.activeSelf && IsPinStanding( pin ) )
            {
                return false;
            }
        }
        return true;
    }

    public void OnStartButtonPressed()
    {

        if( spawnedBall != null )
        {
            Destroy( spawnedBall );
        }

        Vector3 ballPosition = arCamera.transform.position + arCamera.transform.forward * 1.0f;
        ballPosition.y = arCamera.transform.position.y - 0.5f;
        spawnedBall = Instantiate( bowlingBallPrefab, ballPosition, Quaternion.identity );
        spawnedBall.SetActive( true );
        isBallThrown = false;

        if( AreAllPinsKnockedOver() || wasGameReset )
        {
            foreach( GameObject pin in pins )
            {
                pin.SetActive( true );
            }
        }

        startButton.gameObject.SetActive( false );
        resetButton.gameObject.SetActive( true );
    }



    private void ResetBallAndPins()
    {
        ResetBallPosition();
        SetPins( true ); // Resetowanie kr�gli do pozycji pocz�tkowej
        scoreText.text = "Score: " + score;
        throwCounter = 0;
        countedPins.Clear();
        wasGameReset = true;
    }

    private void ResetBallPosition()
    {
        if( spawnedBall != null )
        {
            Destroy( spawnedBall );
        }
        OnStartButtonPressed(); // Ponownie wywo�ujemy, aby zresetowa� kul�
    }

    private void SetPins( bool setActive )
    {
        foreach( GameObject pin in pins )
        {
            pin.SetActive( setActive );
            if( setActive )
            {
                // Resetowanie pozycji i rotacji kr�gla
                Rigidbody rb = pin.GetComponent<Rigidbody>();
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                pin.transform.rotation = Quaternion.Euler( -90, 0, 0 ); // Ustawienie rotacji
                pin.transform.position = GetPinStartPosition( pin ); // Ustawienie pozycji
            }
        }
    }

    private Vector3 GetPinStartPosition( GameObject pin )
    {
        // Zwraca zapisan� pocz�tkow� pozycj� dla danego kr�gla
        int pinIndex = pins.IndexOf( pin );
        return originalPinData[pinIndex].Position;
    }

    private void CheckBallState()
    {
        if( !IsBallWithinPlayArea( spawnedBall.transform.position ) )
        {
            ResetBallPosition();
        }

        //if( isBallThrown && HasBallStopped( spawnedBall ) )
        //{
        //    CheckAndResetPins();
        //    isBallThrown = false;
        //}
    }

    private bool IsBallWithinPlayArea( Vector3 ballPosition )
    {
        // Ustalanie, czy pi�ka jest poza obszarem gry (np. za plansz�)
        return ballPosition.z < -2;
    }
    //OK
    //private void CheckAndResetPins()
    //{
    //    int downPins = 0;
    //    foreach( GameObject pin in pins )
    //    {
    //        if( !IsPinStanding( pin ) )
    //        {
    //            downPins++;
    //            pin.SetActive( false ); // Ukrycie przewr�conego kr�gla
    //        }
    //    }

    //    score += downPins;
    //    scoreText.text = "Score: " + score;

    //    throwCounter++;
    //    if( throwCounter >= 2 || downPins == pins.Count )
    //    {
    //        SetPins( true ); // Resetowanie kr�gli do pozycji pocz�tkowej
    //        throwCounter = 0;
    //    }
    //}

    private void CheckAndResetPins()
    {
        foreach( GameObject pin in pins )
        {
            if( !IsPinStanding( pin ) )
            {
                AddScore( pin );
            }
        }

        if( AreAllPinsKnockedOver() )
        {
            SetPins( true ); // Resetowanie kr�gli do pozycji pocz�tkowej
            scoreText.text = "Score: " + score;
            throwCounter = 0;
        }
    }

    private bool HasBallStopped( GameObject ball )
    {
        Rigidbody ballRigidbody = ball.GetComponent<Rigidbody>();
        return ballRigidbody.velocity.magnitude < 0.01f && ballRigidbody.angularVelocity.magnitude < 0.01f;
    }

    private bool IsPinStanding( GameObject pin )
    {
        const float standingToleranceAngle = 135.0f;
        return Vector3.Angle( Vector3.up, pin.transform.forward ) < standingToleranceAngle;
    }

    private void BallWasThrown()
    {
        isBallThrown = true;
    }

    public void OnBallTouched()
    {
        // Opcjonalna implementacja inicjalizuj�ca rzut kuli
        BallWasThrown();
    }

    //OKKKKK
    //public void AddScore( GameObject pin )
    //{
    //    if( !countedPins.Contains( pin ) )
    //    {
    //        countedPins.Add( pin );
    //        score++;
    //        scoreText.text = "Score: " + score;
    //    }
    //}

    public void AddScore( GameObject pin )
    {
        if( !countedPins.Contains( pin ) )
        {
            countedPins.Add( pin );
            StartCoroutine( HidePinAfterDelay( pin, 1f ) ); // Ukryj kr�giel po 1 sekundzie
            score++;
            scoreText.text = "Score: " + score;
        }
    }


    public bool IsPinKnockedOver( GameObject pin )
    {
        float angle = Vector3.Angle( Vector3.up, pin.transform.forward ); // U�ywamy forward zamiast up
        //Unity u�ywa lewostronnego uk�adu wsp�rz�dnych, a rotacja o -90 stopni w osi x oznacza,
        //�e g�ra kr�gla b�dzie skierowana na "p�noc" w przestrzeni 3D, je�li za��my, �e "p�noc" to kierunek wzrostu osi z.
        return angle > 135f;
    }



    private IEnumerator ResetBallAfterDelay( float delay )
    {
        yield return new WaitForSeconds( delay );
        ResetBallPosition();
        throwCounter++;
        if( throwCounter >= 2 )
        {
            ResetGame();
            throwCounter = 0;
        }
        else
        {
            HideFallenPins();
        }
    }

    private void HideFallenPins()
    {
        foreach( GameObject pin in pins )
        {
            if( IsPinKnockedOver( pin ) )
            {
                StartCoroutine( HidePinAfterDelay( pin, 3f ) );
            }
        }
    }

    private IEnumerator HidePinAfterDelay( GameObject pin, float delay )
    {
        yield return new WaitForSeconds( delay );
        pin.SetActive( false );
    }

    private void ResetGame()
    {
        scoreText.text = "Score: " + score;
        SetPins( true ); // Aktywacja kr�gli
        countedPins.Clear();
    }

    private struct PinData
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public PinData( Vector3 position, Quaternion rotation )
        {
            Position = position;
            Rotation = rotation;
        }
    }
}