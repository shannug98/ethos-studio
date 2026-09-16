import { useEffect } from "react";
import { useLocation } from "react-router-dom";

import BrandIntro from "../components/BrandIntro";
import Hero from "../components/home/Hero";
import Workshops from "../components/home/Workshops";
import About from "../components/home/About";
import Founders from "../components/home/Founders";
import Trainers from "../components/home/Trainers";
import Testimonials from "../components/home/Testimonials";
import ReadyToMove from "../components/home/ReadyToMove";
import InquirySection from "../components/home/InquirySection";
import ShortDanceVideos from "../components/home/ShortDanceVideos";

function Home() {
  const location = useLocation();

  useEffect(() => {
    const targetId =
      location.state?.scrollTo;

    if (!targetId) {
      return;
    }

    const scrollToTarget = () => {
      const element =
        document.getElementById(targetId);

      if (element) {
        element.scrollIntoView({
          behavior: "smooth",
          block: "start",
        });

        window.history.replaceState(
          {},
          document.title
        );
      }
    };

    const timer =
      window.setTimeout(
        scrollToTarget,
        180
      );

    return () =>
      window.clearTimeout(timer);

  }, [location]);

  return (
    <div className="ethos-home-page">

      <BrandIntro />

      <main>

        <Hero />

        <Workshops />

        <ShortDanceVideos />

        <About />

        <Founders />

        <Trainers />

        <Testimonials />

        <ReadyToMove />

        <InquirySection />

      </main>

    </div>
  );
}

export default Home;
