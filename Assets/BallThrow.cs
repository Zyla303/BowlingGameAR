using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class BallThrow : MonoBehaviour
{
    private ARRaycastManager _raycastManager;
    private GameObject _trackedObject;
    private Rigidbody _rigidbody;
    private Vector2 lastTouchPosition;
    private bool isDragging = false;
    private bool isThrown = false;
    private float throwForceMultiplier = 100f; // Dostosuj t� warto�� do potrzeb
    private float maxSpeed = 300f; // Maximum speed limit

    void Awake()
    {
        _raycastManager = FindObjectOfType<ARRaycastManager>();
        _trackedObject = this.gameObject;
        _rigidbody = _trackedObject.GetComponent<Rigidbody>();

        // Na pocz�tku ustaw Rigidbody, aby nie podlega�o zasadom fizyki
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
    }

    void Update()
    {
        if (isThrown)
        {
            // Limit the maximum speed of the ball
            if (_rigidbody.velocity.magnitude > maxSpeed)
            {
                _rigidbody.velocity = _rigidbody.velocity.normalized * maxSpeed;
            }
            return;
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            lastTouchPosition = touch.position;

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    // Sprawd�, czy dotkni�to pi�ki
                    if (DidHitBall(touch.position))
                    {
                        isDragging = true;

                        // Przywr�� pi�k� do stanu, w kt�rym mo�e by� ponownie poruszana
                        _rigidbody.isKinematic = false;
                        _rigidbody.useGravity = true;
                    }
                    break;

                case TouchPhase.Moved:
                    if (isDragging)
                    {
                        // Pod��aj za palcem u�ytkownika
                        TrackFingerMovement(touch);
                    }
                    break;

                case TouchPhase.Ended:
                    if (isDragging)
                    {
                        // Rzu� pi�k�
                        ReleaseBall();
                    }
                    isDragging = false;
                    break;
            }
        }
    }


    private bool DidHitBall(Vector2 touchPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(touchPosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.transform.gameObject == _trackedObject)
            {
                return true;
            }
        }
        return false;
    }

    private void TrackFingerMovement(Touch touch)
    {
        // Przekształcenie pozycji dotknięcia ekranu na pozycję w przestrzeni 3D
        Vector3 screenPosition = new Vector3(touch.position.x, touch.position.y, Camera.main.nearClipPlane + 1);

        // Przy założeniu, że chcemy poruszać się wzdłuż płaszczyzny wykrytej przez ARRaycastManager
        List<ARRaycastHit> hits = new List<ARRaycastHit>();
        if (_raycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;
            _trackedObject.transform.position = hitPose.position;
        }
        else
        {
            // Alternatywnie, użyj dotychczasowej metody, jeżli promień nie trafił w żaden płaski obszar
            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);
            _trackedObject.transform.position = new Vector3(worldPosition.x, worldPosition.y, _trackedObject.transform.position.z);
        }
    }



    private void ReleaseBall()
    {
        // Odblokuj zasady fizyki
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;

        // Pobierz wektor kierunku kamery AR
        Vector3 cameraForward = Camera.main.transform.forward;
        cameraForward.y = 0; // Ignoruj składowe pionowe, aby ruch był poziomy

        // Normalizuj wektor kierunku kamery
        Vector3 throwDirection = cameraForward.normalized;

        // Pobierz aktualne przyspieszenie urzadzenia, ale zignoruj jego kierunek
        // Zamiast tego użyj wektora kierunku kamery
        float throwForceMagnitude = Input.acceleration.magnitude * throwForceMultiplier * 0.5f; // Zmniejsz siłę o połowę

        // Aplikuj siłę rzutu
        _rigidbody.AddForce(throwDirection * throwForceMagnitude, ForceMode.Impulse);

        // Ustaw flagę, że piłka została rzucona
        isThrown = true;
    }

}
