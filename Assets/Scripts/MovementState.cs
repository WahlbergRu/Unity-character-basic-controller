namespace Character.Assets.Scripts
{
    public enum MovementState
    {
        idle,
        walking = 1,
        sprinting = 2,
        rotate = 3,
        rolling = 4,
        jumping = 5,
        landing = 6,
        transit = 7,
        wasGrounded = 8,
    }
}