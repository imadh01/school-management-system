import { useState } from "react";

export function useDarkMode() {
  const [isDark, setIsDark] = useState(false);

  const toggle = () => {
    const next = document.body.classList.toggle("dark-mode");
    setIsDark(next);
  };

  return { isDark, toggle };
}
