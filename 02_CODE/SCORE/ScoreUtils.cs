public static class ScoreUtils
{
    public static float NextCombo(float current)
    {
        if (current < 1.2f) return 1.2f;
        if (current < 1.5f) return 1.5f;
        if (current < 2.0f) return 2.0f;
        if (current < 3.0f) return 3.0f;
        return 3.0f;
    }
}