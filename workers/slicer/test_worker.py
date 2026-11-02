import unittest
from worker import parse_summary


def fixture(grams="12.50", time="1h 2m 3s"):
    return (f"; filament used [g] = {grams}\n; total filament used [g] = {grams}\n"
            "; total filament used for wipe tower [g] = 2.00\n"
            f"; estimated printing time (normal mode) = {time}\n"
            "; prusaslicer_config = begin\n; layer_height = 0.2\n; prusaslicer_config = end\n")


class SummaryTests(unittest.TestCase):
    def test_total_is_not_double_counted(self):
        result = parse_summary(fixture())
        self.assertEqual(result["grams"], 12.5)
        self.assertEqual(result["seconds"], 3723)
        self.assertEqual(result, parse_summary(fixture()))

    def test_missing_ambiguous_and_invalid_fail_closed(self):
        for text in ["", fixture() + fixture(), fixture("0"), fixture("999999"),
                     fixture(time="unknown"), fixture().replace("12.50\n; total", "9.00\n; total", 1),
                     fixture().replace("; filament used [g] = 12.50", "; filament used [g] = 6.25, 6.25")]:
            with self.assertRaises(ValueError):
                parse_summary(text)

    def test_days_and_config_identity(self):
        a = parse_summary(fixture(time="1d 1s"))
        b = parse_summary(fixture(time="1d 1s").replace("0.2", "0.3"))
        self.assertEqual(a["seconds"], 86401)
        self.assertNotEqual(a["effectiveConfigHash"], b["effectiveConfigHash"])


if __name__ == "__main__":
    unittest.main()
