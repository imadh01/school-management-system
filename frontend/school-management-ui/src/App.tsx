import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "./store/AuthContext";
import { ThemeProvider } from "./store/ThemeContext";
import { ToastProvider } from "./components/Toast";
import { AppRoutes } from "./routes/AppRoutes";

function App() {
  return (
    <ThemeProvider>
      <ToastProvider>
        <AuthProvider>
          <BrowserRouter>
            <AppRoutes />
          </BrowserRouter>
        </AuthProvider>
      </ToastProvider>
    </ThemeProvider>
  );
}

export default App;
