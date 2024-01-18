using UnityEngine;

public class PinCollision : MonoBehaviour
{
    public BowlingAlleyPlacer alleyPlacer;

    void OnCollisionEnter( Collision collision )
    {
        // Sprawd�, czy obiekt, z kt�rym zderzy� si� kr�giel, to pi�ka lub inny kr�giel
        if( alleyPlacer.IsPinKnockedOver( gameObject ) && 
            collision.gameObject.CompareTag( "BowlingBall" ) || 
            collision.gameObject.CompareTag( "Pin" ) )
        {
            alleyPlacer.AddScore( gameObject );
        }
    }
}
