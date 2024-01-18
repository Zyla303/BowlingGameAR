using UnityEngine;

public class PinCollision : MonoBehaviour
{
    public BowlingAlleyPlacer alleyPlacer;

    void OnCollisionEnter( Collision collision )
    {
        // SprawdŸ, czy obiekt, z którym zderzy³ siê krêgiel, to pi³ka lub inny krêgiel
        if( alleyPlacer.IsPinKnockedOver( gameObject ) && 
            collision.gameObject.CompareTag( "BowlingBall" ) || 
            collision.gameObject.CompareTag( "Pin" ) )
        {
            alleyPlacer.AddScore( gameObject );
        }
    }
}
