using System;
using System.Collections.Generic;
using System.Linq;

public class SpeechPostProcessing
{
    private static Dictionary<string, string> nameCorrections = new Dictionary<string, string>
    {
        {"wahyu andika", "Ir. Wahyu Andhyka Kusuma, S.Kom., M.Kom."},
        {"aminuddin", "Aminuddin, S.Kom., M.Cs."},
        {"briansyah setio", "Briansyah Setio Wiyono, S.Kom., M.Kom."},
        {"evi dwi", "Evi Dwi Wahyuni, S.Kom., M.Kom."},
        {"gita indah", "Gita Indah Marthasari, S.T., M.Kom."},
        {"hariyadi", "Hariyadi, S.Kom., M.T."},
        {"ilyas nuryasin", "Ilyas Nuryasin, S.Kom., M.Kom."},
        {"wildan suharso", "Wildan Suharso, S.Kom., M.Kom."},
        {"yuan aulia", "Yuan Aulia Rahma, S.Kom., M.Kom."},
        {"agus eko", "Ir. Agus Eko Minarno, S.Kom., M.Kom."},
        {"christian sri", "Christian Sri Kusuma Aditya, S.Kom., M.Kom."},
        {"didih rizki", "Didih Rizki Chandranegara, S.Kom., M.Kom."},
        {"4 kali", "Galih Wasis Wicaksono, S.Kom., M.Cs."},
        {"lailis syafaah", "Lailis Syafa'ah, M.T."},
        {"nur hayatin", "Nur Hayatin, S.ST., M.Kom."},
        {"setio basuki", "Setio Basuki, S.T., M.T., Ph.D."},
        {"vinna rahmayanti", "Vinna Rahmayanti SN, S.Si., M.Si."},
        {"yuda munarko", "Yuda Munarko, S.Kom., M.Sc."},
        {"yufis azhar", "Yufis Azhar, S.Kom., M.Kom."}
    };

    public static string CorrectNames(string transcribedText)
    {
        string correctedText = transcribedText.ToLower();

        foreach (var entry in nameCorrections)
        {
            if (correctedText.Contains(entry.Key))
            {
                correctedText = correctedText.Replace(entry.Key, entry.Value);
            }
            else
            {
                string closestMatch = FindClosestMatch(entry.Key, correctedText);
                if (closestMatch != null)
                {
                    correctedText = correctedText.Replace(closestMatch, entry.Value);
                }
            }
        }

        return correctedText;
    }

    private static string FindClosestMatch(string correctName, string text)
    {
        string[] words = text.Split(' ');
        string closestMatch = null;
        int minDistance = 2; // Ambang batas toleransi kesalahan (bisa disesuaikan)

        foreach (var word in words)
        {
            int distance = LevenshteinDistance(word, correctName);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestMatch = word;
            }
        }

        return closestMatch;
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        int len1 = s1.Length;
        int len2 = s2.Length;
        int[,] dp = new int[len1 + 1, len2 + 1];

        for (int i = 0; i <= len1; i++)
            dp[i, 0] = i;
        for (int j = 0; j <= len2; j++)
            dp[0, j] = j;

        for (int i = 1; i <= len1; i++)
        {
            for (int j = 1; j <= len2; j++)
            {
                int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
            }
        }

        return dp[len1, len2];
    }
}
