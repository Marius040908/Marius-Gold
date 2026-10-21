import unittest
from datetime import date, time, datetime, timedelta
from time_machine import run_analysis, evaluate_path, make_pool

class TimeMachineTests(unittest.TestCase):
    def sessions(self, n=30):
        result = {}
        for i in range(n):
            d = date(2026, 6, 1) + timedelta(days=i)
            rows = []
            p = 100.0
            for j in range(78):
                t = (datetime.combine(d,time(9,30)) + timedelta(minutes=5*j)).time()
                new_p = p * (1 + 0.0005 * ((j+i)%7-3))
                rows.append((t,p,max(p,new_p)*1.001,min(p,new_p)*.999,new_p))
                p = new_p
            result[d] = rows
        return result
    def test_stop_priority(self):
        x = evaluate_path(100,99,103,"long",[(1,1.04,.98,1)])
        self.assertEqual(x[0],"stop")
    def test_reproducibility(self):
        kwargs = dict(entry=100,stop=99,target=103,side="long",
                      entry_clock=time(10,15),close_clock=time(15,55),replays=200,seed=19)
        a = run_analysis(self.sessions(),**kwargs)
        b = run_analysis(self.sessions(),**kwargs)
        self.assertEqual(a,b)
        self.assertEqual(round(sum(a["probabilities_pct"].values())),100)
        self.assertEqual(a["clock_aligned_history"]["count"],30)
    def test_minimum_history(self):
        with self.assertRaises(ValueError):
            run_analysis(self.sessions(10),100,99,103,"long",time(10,15),time(15,55))
    def test_invalid_long_order(self):
        with self.assertRaises(ValueError):
            run_analysis(self.sessions(),100,101,103,"long",time(10,15),time(15,55))
    def test_short(self):
        x = run_analysis(self.sessions(),100,101,97,"short",time(10,15),time(15,55),replays=50)
        self.assertEqual(x["simulations"],50)

if __name__ == "__main__":
    unittest.main()
